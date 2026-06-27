using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NetIAM.Infrastructure.Services;

public interface ISamlCertificateService
{
    Task<SamlSigningCertificateSet> GetSigningCertificateSetAsync(string tenantId, CancellationToken cancellationToken = default);
}

public sealed record SamlSigningCertificateSet(
    X509Certificate2 ActiveCertificate,
    IReadOnlyCollection<X509Certificate2> MetadataCertificates);

internal sealed record SamlSigningCertificateRegistry(
    IReadOnlyCollection<SamlSigningCertificateSnapshot> Certificates);

internal sealed record SamlSigningCertificateSnapshot(
    string Id,
    string PfxBase64,
    string Password,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    bool IsPrimary);

public sealed class SamlCertificateService(ISystemSettingStore settingStore) : ISamlCertificateService
{
    private const string SettingKey = "saml.idp.signing-certificates";
    private static readonly TimeSpan CertificateValidity = TimeSpan.FromDays(180);
    private static readonly TimeSpan RolloverLeadTime = TimeSpan.FromDays(21);
    private static readonly TimeSpan RolloverOverlap = TimeSpan.FromDays(14);
    private static readonly TimeSpan RetentionAfterExpiry = TimeSpan.FromDays(45);

    public async Task<SamlSigningCertificateSet> GetSigningCertificateSetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var registry = await settingStore.GetAsync(
            tenantId,
            SettingKey,
            new SamlSigningCertificateRegistry(Array.Empty<SamlSigningCertificateSnapshot>()),
            cancellationToken);
        var snapshots = registry.Certificates.ToList();
        var changed = false;

        if (snapshots.Count == 0)
        {
            snapshots.Add(CreateSnapshot($"netiam-saml-{tenantId}-current", now.AddMinutes(-5), now.Add(CertificateValidity), isPrimary: true));
            changed = true;
        }

        var primary = ResolvePrimarySnapshot(snapshots, now);
        if (primary is null)
        {
            for (var index = 0; index < snapshots.Count; index++)
            {
                snapshots[index] = snapshots[index] with { IsPrimary = false };
            }

            var fallback = CreateSnapshot($"netiam-saml-{tenantId}-fallback", now.AddMinutes(-5), now.Add(CertificateValidity), isPrimary: true);
            snapshots.Add(fallback);
            primary = fallback;
            changed = true;
        }

        var needsNext = primary.NotAfter <= now.Add(RolloverLeadTime);
        var hasUpcoming = snapshots.Any(x => x.Id != primary.Id && x.NotAfter > now && x.NotBefore > now.AddDays(-1));
        if (needsNext && !hasUpcoming)
        {
            var nextNotBefore = primary.NotAfter.Subtract(RolloverOverlap);
            if (nextNotBefore < now.AddMinutes(-5))
            {
                nextNotBefore = now.AddMinutes(-5);
            }

            var nextNotAfter = nextNotBefore.Add(CertificateValidity);
            snapshots.Add(CreateSnapshot($"netiam-saml-{tenantId}-next", nextNotBefore, nextNotAfter, isPrimary: false));
            changed = true;
        }

        if (primary.NotAfter <= now)
        {
            var promotionCandidate = snapshots
                .Where(x => x.Id != primary.Id && x.NotBefore <= now && x.NotAfter > now)
                .OrderBy(x => x.NotBefore)
                .FirstOrDefault();
            if (promotionCandidate is not null)
            {
                snapshots = snapshots
                    .Select(x => x with { IsPrimary = x.Id == promotionCandidate.Id })
                    .ToList();
                primary = snapshots.First(x => x.Id == promotionCandidate.Id);
                changed = true;
            }
        }

        var beforeCleanupCount = snapshots.Count;
        snapshots = snapshots
            .Where(x => x.IsPrimary || x.NotAfter >= now.Subtract(RetentionAfterExpiry))
            .ToList();
        if (beforeCleanupCount != snapshots.Count)
        {
            changed = true;
        }

        if (changed)
        {
            await settingStore.SetAsync(
                tenantId,
                SettingKey,
                new SamlSigningCertificateRegistry(snapshots),
                cancellationToken);
        }

        primary = ResolvePrimarySnapshot(snapshots, now)
            ?? throw new InvalidOperationException("SAML signing certificate primary selection failed.");

        var activeCertificate = ToCertificate(primary);
        var metadataCertificates = snapshots
            .Where(x => x.NotAfter > now && (x.IsPrimary || x.NotBefore <= now.Add(RolloverLeadTime)))
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.NotBefore)
            .Select(ToCertificate)
            .ToArray();

        if (metadataCertificates.Length == 0)
        {
            metadataCertificates = [activeCertificate];
        }

        return new SamlSigningCertificateSet(activeCertificate, metadataCertificates);
    }

    private static SamlSigningCertificateSnapshot? ResolvePrimarySnapshot(
        IReadOnlyCollection<SamlSigningCertificateSnapshot> snapshots,
        DateTimeOffset now)
    {
        var primary = snapshots.FirstOrDefault(x => x.IsPrimary && x.NotAfter > now);
        if (primary is not null)
        {
            return primary;
        }

        return snapshots
            .Where(x => x.NotAfter > now)
            .OrderByDescending(x => x.NotBefore)
            .FirstOrDefault();
    }

    private static SamlSigningCertificateSnapshot CreateSnapshot(
        string subjectSuffix,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool isPrimary)
    {
        using var rsa = RSA.Create(3072);
        var subjectName = new X500DistinguishedName($"CN={subjectSuffix}");
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        using var certificate = request.CreateSelfSigned(notBefore.UtcDateTime, notAfter.UtcDateTime);
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var pfx = certificate.Export(X509ContentType.Pkcs12, password);

        return new SamlSigningCertificateSnapshot(
            Guid.NewGuid().ToString("N"),
            Convert.ToBase64String(pfx),
            password,
            notBefore,
            notAfter,
            isPrimary);
    }

    private static X509Certificate2 ToCertificate(SamlSigningCertificateSnapshot snapshot)
    {
        var pfx = Convert.FromBase64String(snapshot.PfxBase64);
        return X509CertificateLoader.LoadPkcs12(
            pfx,
            snapshot.Password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }
}
