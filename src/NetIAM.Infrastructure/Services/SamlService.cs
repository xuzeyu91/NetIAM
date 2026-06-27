using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Security.Claims;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public interface ISamlService
{
    Task<string> BuildMetadataXmlAsync(string tenantId, string baseUri, CancellationToken cancellationToken = default);

    Task<SamlSsoResponse> BuildSsoResponseAsync(SamlSsoRequest request, CancellationToken cancellationToken = default);

    Task<string> BuildHttpPostHtmlAsync(SamlSsoRequest request, CancellationToken cancellationToken = default);
}

public sealed class SamlService(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager,
    ISamlCertificateService samlCertificateService) : ISamlService
{
    private const string SamlProtocol = "urn:oasis:names:tc:SAML:2.0:protocol";
    private const string SamlAssertion = "urn:oasis:names:tc:SAML:2.0:assertion";
    private const string SamlMetadata = "urn:oasis:names:tc:SAML:2.0:metadata";
    private const string XmlSignatureNamespace = SignedXml.XmlDsigNamespaceUrl;

    public async Task<string> BuildMetadataXmlAsync(string tenantId, string baseUri, CancellationToken cancellationToken = default)
    {
        var md = XNamespace.Get(SamlMetadata);
        var entityId = $"{baseUri.TrimEnd('/')}/saml2/metadata/{tenantId}";
        var tenantSpCount = await dbContext.SamlServiceProviders.CountAsync(
            x => x.TenantId == tenantId && x.Enabled && !x.IsDeleted,
            cancellationToken);
        var certificateSet = await samlCertificateService.GetSigningCertificateSetAsync(tenantId, cancellationToken);
        var metadataCertificates = certificateSet.MetadataCertificates
            .GroupBy(x => x.Thumbprint ?? Convert.ToBase64String(x.RawData), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        var idpDescriptor = new XElement(md + "IDPSSODescriptor",
            new XAttribute("protocolSupportEnumeration", SamlProtocol),
            new XElement(md + "SingleSignOnService",
                new XAttribute("Binding", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"),
                new XAttribute("Location", $"{baseUri.TrimEnd('/')}/saml2/sso/default")),
            new XElement(md + "SingleSignOnService",
                new XAttribute("Binding", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect"),
                new XAttribute("Location", $"{baseUri.TrimEnd('/')}/saml2/sso/default")),
            new XElement(md + "NameIDFormat", "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"),
            new XElement(md + "Organization",
                new XElement(md + "OrganizationName", new XAttribute(XNamespace.Xml + "lang", "en"), "NetIAM"),
                new XElement(md + "OrganizationDisplayName", new XAttribute(XNamespace.Xml + "lang", "en"), "NetIAM Identity Provider"),
                new XElement(md + "OrganizationURL", new XAttribute(XNamespace.Xml + "lang", "en"), baseUri)),
            new XElement(md + "Extensions",
                new XElement("NetIamEnabledServiceProviders", tenantSpCount)));

        foreach (var metadataCertificate in metadataCertificates)
        {
            idpDescriptor.Add(
                new XElement(md + "KeyDescriptor",
                    new XAttribute("use", "signing"),
                    new XElement(XName.Get("KeyInfo", XmlSignatureNamespace),
                        new XElement(XName.Get("X509Data", XmlSignatureNamespace),
                            new XElement(XName.Get("X509Certificate", XmlSignatureNamespace), Convert.ToBase64String(metadataCertificate.RawData))))));
        }

        var validUntil = metadataCertificates
            .OrderBy(x => x.NotAfter)
            .First()
            .NotAfter
            .ToUniversalTime()
            .ToString("o");
        var descriptor = new XElement(md + "EntityDescriptor",
            new XAttribute("entityID", entityId),
            new XAttribute("validUntil", validUntil),
            idpDescriptor);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), descriptor);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    public async Task<SamlSsoResponse> BuildSsoResponseAsync(SamlSsoRequest request, CancellationToken cancellationToken = default)
    {
        var serviceProvider = await dbContext.SamlServiceProviders
            .FirstOrDefaultAsync(
                x => x.TenantId == request.TenantId
                     && x.Code == request.ServiceProviderCode
                     && x.Enabled
                     && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException($"SAML service provider not found: {request.ServiceProviderCode}.");

        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Id == request.UserId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"User not found for SAML response: {request.UserId}.");

        var issuer = $"netiam:{request.TenantId}:idp";
        var now = DateTimeOffset.UtcNow;
        var responseId = $"_{Guid.NewGuid():N}";
        var assertionId = $"_{Guid.NewGuid():N}";
        var nameId = string.IsNullOrWhiteSpace(user.ExternalId) ? user.UserName! : user.ExternalId;
        var signingCertificateSet = await samlCertificateService.GetSigningCertificateSetAsync(request.TenantId, cancellationToken);
        var signingCertificate = signingCertificateSet.ActiveCertificate;

        var samlp = XNamespace.Get(SamlProtocol);
        var saml = XNamespace.Get(SamlAssertion);

        var responseXml = new XElement(samlp + "Response",
            new XAttribute("ID", responseId),
            new XAttribute("Version", "2.0"),
            new XAttribute("IssueInstant", now.ToString("o")),
            new XAttribute("Destination", serviceProvider.AssertionConsumerServiceUrl),
            new XElement(saml + "Issuer", issuer),
            new XElement(samlp + "Status",
                new XElement(samlp + "StatusCode",
                    new XAttribute("Value", "urn:oasis:names:tc:SAML:2.0:status:Success"))),
            new XElement(saml + "Assertion",
                new XAttribute("ID", assertionId),
                new XAttribute("Version", "2.0"),
                new XAttribute("IssueInstant", now.ToString("o")),
                new XElement(saml + "Issuer", issuer),
                new XElement(saml + "Subject",
                    new XElement(saml + "NameID",
                        new XAttribute("Format", serviceProvider.NameIdFormat),
                        nameId),
                    new XElement(saml + "SubjectConfirmation",
                        new XAttribute("Method", "urn:oasis:names:tc:SAML:2.0:cm:bearer"),
                        new XElement(saml + "SubjectConfirmationData",
                            new XAttribute("NotOnOrAfter", now.AddMinutes(5).ToString("o")),
                            new XAttribute("Recipient", serviceProvider.AssertionConsumerServiceUrl)))),
                new XElement(saml + "Conditions",
                    new XAttribute("NotBefore", now.AddMinutes(-2).ToString("o")),
                    new XAttribute("NotOnOrAfter", now.AddMinutes(5).ToString("o")),
                    new XElement(saml + "AudienceRestriction",
                        new XElement(saml + "Audience", serviceProvider.Audience ?? serviceProvider.EntityId))),
                new XElement(saml + "AuthnStatement",
                    new XAttribute("AuthnInstant", now.ToString("o")),
                    new XElement(saml + "AuthnContext",
                        new XElement(saml + "AuthnContextClassRef", "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport"))),
                new XElement(saml + "AttributeStatement",
                    new XElement(saml + "Attribute",
                        new XAttribute("Name", ClaimTypes.NameIdentifier),
                        new XElement(saml + "AttributeValue", user.Id)),
                    new XElement(saml + "Attribute",
                        new XAttribute("Name", ClaimTypes.Name),
                        new XElement(saml + "AttributeValue", user.DisplayName)),
                    new XElement(saml + "Attribute",
                        new XAttribute("Name", ClaimTypes.Email),
                        new XElement(saml + "AttributeValue", user.Email ?? string.Empty)))));

        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), responseXml);
        var xmlDocument = ToXmlDocument(document);
        if (serviceProvider.WantSignedAssertions)
        {
            SignAssertion(xmlDocument, signingCertificate);
        }

        if (!string.IsNullOrWhiteSpace(serviceProvider.SigningCertificatePem))
        {
            var encryptionCertificate = ParsePemCertificate(serviceProvider.SigningCertificatePem);
            EncryptAssertion(xmlDocument, encryptionCertificate);
        }

        var xmlBytes = Encoding.UTF8.GetBytes(xmlDocument.OuterXml);
        var encodedResponse = Convert.ToBase64String(xmlBytes);

        return new SamlSsoResponse(
            issuer,
            serviceProvider.AssertionConsumerServiceUrl,
            nameId,
            serviceProvider.Audience ?? serviceProvider.EntityId,
            encodedResponse);
    }

    public async Task<string> BuildHttpPostHtmlAsync(SamlSsoRequest request, CancellationToken cancellationToken = default)
    {
        var samlResponse = await BuildSsoResponseAsync(request, cancellationToken);
        var serviceProvider = await dbContext.SamlServiceProviders
            .AsNoTracking()
            .FirstAsync(
                x => x.TenantId == request.TenantId
                     && x.Code == request.ServiceProviderCode
                     && x.Enabled
                     && !x.IsDeleted,
                cancellationToken);

        var relayState = request.RelayState
            ?? serviceProvider.RelayStateDefault
            ?? string.Empty;

        return $"""
                <!doctype html>
                <html>
                <head><meta charset="utf-8"><title>NetIAM SAML Redirect</title></head>
                <body onload="document.forms[0].submit()">
                  <form method="post" action="{WebUtility.HtmlEncode(serviceProvider.AssertionConsumerServiceUrl)}">
                    <input type="hidden" name="SAMLResponse" value="{WebUtility.HtmlEncode(samlResponse.EncodedSamlResponse)}" />
                    <input type="hidden" name="RelayState" value="{WebUtility.HtmlEncode(relayState)}" />
                    <noscript><button type="submit">Continue</button></noscript>
                  </form>
                </body>
                </html>
                """;
    }

    private static XmlDocument ToXmlDocument(XDocument document)
    {
        var xmlDocument = new XmlDocument
        {
            PreserveWhitespace = true
        };
        xmlDocument.LoadXml(document.ToString(SaveOptions.DisableFormatting));
        return xmlDocument;
    }

    private static void SignAssertion(XmlDocument xmlDocument, X509Certificate2 signingCertificate)
    {
        var assertion = xmlDocument.GetElementsByTagName("Assertion", SamlAssertion)
            .OfType<XmlElement>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("SAML assertion node not found.");
        var assertionId = assertion.GetAttribute("ID");
        if (string.IsNullOrWhiteSpace(assertionId))
        {
            throw new InvalidOperationException("SAML assertion ID is required for signing.");
        }

        using var signingKey = signingCertificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("SAML signing certificate private key not available.");
        var signedXml = new SignedXml(assertion)
        {
            SigningKey = signingKey
        };
        if (signedXml.SignedInfo is null)
        {
            throw new InvalidOperationException("SAML signature generation failed to initialize SignedInfo.");
        }

        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        var reference = new Reference($"#{assertionId}")
        {
            DigestMethod = SignedXml.XmlDsigSHA256Url
        };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(signingCertificate));
        signedXml.KeyInfo = keyInfo;
        signedXml.ComputeSignature();

        var signatureElement = signedXml.GetXml();
        var importedSignature = xmlDocument.ImportNode(signatureElement, true);
        assertion.InsertAfter(importedSignature, assertion.FirstChild);
    }

    private static void EncryptAssertion(XmlDocument xmlDocument, X509Certificate2 encryptionCertificate)
    {
        var assertion = xmlDocument.GetElementsByTagName("Assertion", SamlAssertion)
            .OfType<XmlElement>()
            .FirstOrDefault();
        if (assertion is null)
        {
            return;
        }

        using var rsa = encryptionCertificate.GetRSAPublicKey()
            ?? throw new InvalidOperationException("SAML SP encryption certificate missing RSA public key.");
        using var aes = Aes.Create();
        aes.KeySize = 256;

        var encryptedXml = new EncryptedXml();
        var encryptedElement = encryptedXml.EncryptData(assertion, aes, content: false);

        var encryptedData = new EncryptedData
        {
            Type = EncryptedXml.XmlEncElementUrl,
            EncryptionMethod = new EncryptionMethod(EncryptedXml.XmlEncAES256Url),
            CipherData = new CipherData(encryptedElement)
        };

        var encryptedKey = new EncryptedKey
        {
            EncryptionMethod = new EncryptionMethod(EncryptedXml.XmlEncRSAOAEPUrl),
            CipherData = new CipherData(EncryptedXml.EncryptKey(aes.Key, rsa, useOAEP: true))
        };
        encryptedData.KeyInfo = new KeyInfo();
        encryptedData.KeyInfo.AddClause(new KeyInfoEncryptedKey(encryptedKey));

        var encryptedAssertion = xmlDocument.CreateElement("saml", "EncryptedAssertion", SamlAssertion);
        encryptedAssertion.AppendChild(xmlDocument.ImportNode(encryptedData.GetXml(), true));

        assertion.ParentNode?.ReplaceChild(encryptedAssertion, assertion);
    }

    private static X509Certificate2 ParsePemCertificate(string certificatePem)
    {
        var parsed = X509Certificate2.CreateFromPem(certificatePem);
        return X509CertificateLoader.LoadCertificate(parsed.Export(X509ContentType.Cert));
    }
}
