using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Abstractions;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Identity;
using OpenIddict.EntityFrameworkCore.Models;

namespace NetIAM.Infrastructure.Persistence;

public sealed class NetIamDbContext(DbContextOptions<NetIamDbContext> options)
    : IdentityDbContext<NetIamIdentityUser, IdentityRole, string>(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    public DbSet<OrganizationMemberEntity> OrganizationMembers => Set<OrganizationMemberEntity>();

    public DbSet<UserGroupEntity> UserGroups => Set<UserGroupEntity>();

    public DbSet<UserGroupMemberEntity> UserGroupMembers => Set<UserGroupMemberEntity>();

    public DbSet<IdentityProviderEntity> IdentityProviders => Set<IdentityProviderEntity>();

    public DbSet<IdentitySourceEntity> IdentitySources => Set<IdentitySourceEntity>();

    public DbSet<ThirdPartyUserEntity> ThirdPartyUsers => Set<ThirdPartyUserEntity>();

    public DbSet<UserIdpBindEntity> UserIdpBinds => Set<UserIdpBindEntity>();

    public DbSet<AppEntity> Apps => Set<AppEntity>();

    public DbSet<AppAccessPolicyEntity> AppAccessPolicies => Set<AppAccessPolicyEntity>();

    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();

    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();

    public DbSet<UserPermissionGrantEntity> UserPermissionGrants => Set<UserPermissionGrantEntity>();

    public DbSet<IdentitySourceSyncHistoryEntity> IdentitySourceSyncHistories => Set<IdentitySourceSyncHistoryEntity>();

    public DbSet<IdentitySourceSyncRecordEntity> IdentitySourceSyncRecords => Set<IdentitySourceSyncRecordEntity>();

    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    public DbSet<SamlServiceProviderEntity> SamlServiceProviders => Set<SamlServiceProviderEntity>();

    public DbSet<ScimAccessTokenEntity> ScimAccessTokens => Set<ScimAccessTokenEntity>();

    public DbSet<SystemSettingEntity> SystemSettings => Set<SystemSettingEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        ConfigureIdentity(builder);
        ConfigureTenancy(builder);
        ConfigureOrganizations(builder);
        ConfigureGroups(builder);
        ConfigureAuthProviders(builder);
        ConfigureAuthBindings(builder);
        ConfigureApplications(builder);
        ConfigureRbac(builder);
        ConfigureSync(builder);
        ConfigureAudit(builder);
        ConfigureSamlScim(builder);
        ConfigureSystemSettings(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<NetIamIdentityUser>(entity =>
        {
            entity.ToTable("eiam_user");
            entity.Property(p => p.Id).HasColumnName("id_").HasMaxLength(64).IsRequired();
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.UserName).HasColumnName("username_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
            entity.Property(p => p.NormalizedUserName).HasColumnName("normalized_username").HasMaxLength(128);
            entity.Property(p => p.Email).HasColumnName("email_").HasMaxLength(256);
            entity.Property(p => p.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256);
            entity.Property(p => p.EmailConfirmed).HasColumnName("email_confirmed");
            entity.Property(p => p.PasswordHash).HasColumnName("password_hash").HasMaxLength(512);
            entity.Property(p => p.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(256);
            entity.Property(p => p.ConcurrencyStamp).HasColumnName("concurrency_stamp").HasMaxLength(256);
            entity.Property(p => p.PhoneNumber).HasColumnName("phone_number").HasMaxLength(64);
            entity.Property(p => p.PhoneNumberConfirmed).HasColumnName("phone_confirmed");
            entity.Property(p => p.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(p => p.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(p => p.LockoutEnabled).HasColumnName("lockout_enabled");
            entity.Property(p => p.AccessFailedCount).HasColumnName("access_failed_count");
            entity.Property(p => p.ExternalId).HasColumnName("external_id").HasMaxLength(128);
            entity.Property(p => p.DataOrigin).HasColumnName("data_origin").HasConversion<int>();
            entity.Property(p => p.CreateBy).HasColumnName("create_by").HasMaxLength(64);
            entity.Property(p => p.CreateTime).HasColumnName("create_time");
            entity.Property(p => p.UpdateBy).HasColumnName("update_by").HasMaxLength(64);
            entity.Property(p => p.UpdateTime).HasColumnName("update_time");
            entity.Property(p => p.IsDeleted).HasColumnName("is_deleted");
            entity.Property(p => p.Remark).HasColumnName("remark_").HasMaxLength(512);
            entity.HasIndex(p => new { p.TenantId, p.UserName }).IsUnique();
            entity.HasIndex(p => new { p.TenantId, p.ExternalId });
        });

        builder.Entity<IdentityRole>(entity =>
        {
            entity.ToTable("eiam_role");
            entity.Property(p => p.Id).HasColumnName("id_");
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128);
            entity.Property(p => p.NormalizedName).HasColumnName("normalized_name").HasMaxLength(128);
            entity.Property(p => p.ConcurrencyStamp).HasColumnName("concurrency_stamp").HasMaxLength(256);
        });

        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.ToTable("eiam_user_role");
            entity.Property(p => p.UserId).HasColumnName("user_id");
            entity.Property(p => p.RoleId).HasColumnName("role_id");
        });

        builder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.ToTable("eiam_user_claim");
            entity.Property(p => p.Id).HasColumnName("id_");
            entity.Property(p => p.UserId).HasColumnName("user_id");
            entity.Property(p => p.ClaimType).HasColumnName("claim_type");
            entity.Property(p => p.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("eiam_user_login");
            entity.Property(p => p.LoginProvider).HasColumnName("login_provider");
            entity.Property(p => p.ProviderKey).HasColumnName("provider_key");
            entity.Property(p => p.ProviderDisplayName).HasColumnName("provider_display_name");
            entity.Property(p => p.UserId).HasColumnName("user_id");
        });

        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("eiam_user_token");
            entity.Property(p => p.UserId).HasColumnName("user_id");
            entity.Property(p => p.LoginProvider).HasColumnName("login_provider");
            entity.Property(p => p.Name).HasColumnName("name_");
            entity.Property(p => p.Value).HasColumnName("value_");
        });

        builder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.ToTable("eiam_role_claim");
            entity.Property(p => p.Id).HasColumnName("id_");
            entity.Property(p => p.RoleId).HasColumnName("role_id");
            entity.Property(p => p.ClaimType).HasColumnName("claim_type");
            entity.Property(p => p.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<OpenIddictEntityFrameworkCoreApplication>(entity => entity.ToTable("eiam_oidc_application"));
        builder.Entity<OpenIddictEntityFrameworkCoreAuthorization>(entity => entity.ToTable("eiam_oidc_authorization"));
        builder.Entity<OpenIddictEntityFrameworkCoreScope>(entity => entity.ToTable("eiam_oidc_scope"));
        builder.Entity<OpenIddictEntityFrameworkCoreToken>(entity => entity.ToTable("eiam_oidc_token"));
    }

    private static void ConfigureTenancy(ModelBuilder builder)
    {
        builder.Entity<TenantEntity>(entity =>
        {
            entity.ToTable("eiam_tenant");
            entity.Property(p => p.Identifier).HasColumnName("identifier_").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.IsActive).HasColumnName("is_active");
            entity.Property(p => p.DefaultDomain).HasColumnName("default_domain").HasMaxLength(256);
            entity.HasIndex(p => p.Identifier).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureOrganizations(ModelBuilder builder)
    {
        builder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("eiam_organization");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Code).HasColumnName("code_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.ParentId).HasColumnName("parent_id").HasMaxLength(64);
            entity.Property(p => p.Path).HasColumnName("path_").HasMaxLength(512).IsRequired();
            entity.Property(p => p.DisplayPath).HasColumnName("display_path").HasMaxLength(1024).IsRequired();
            entity.Property(p => p.ExternalId).HasColumnName("external_id").HasMaxLength(128);
            entity.Property(p => p.IdentitySourceId).HasColumnName("identity_source_id").HasMaxLength(64);
            entity.Property(p => p.DataOrigin).HasColumnName("data_origin").HasConversion<int>();
            entity.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            entity.HasIndex(p => new { p.TenantId, p.ExternalId });
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<OrganizationMemberEntity>(entity =>
        {
            entity.ToTable("eiam_organization_member");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.OrganizationId).HasColumnName("organization_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
            entity.HasIndex(p => new { p.TenantId, p.OrganizationId, p.UserId }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureGroups(ModelBuilder builder)
    {
        builder.Entity<UserGroupEntity>(entity =>
        {
            entity.ToTable("eiam_user_group");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Description).HasColumnName("description_").HasMaxLength(512);
            entity.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<UserGroupMemberEntity>(entity =>
        {
            entity.ToTable("eiam_user_group_member");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.UserGroupId).HasColumnName("user_group_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
            entity.HasIndex(p => new { p.TenantId, p.UserGroupId, p.UserId }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureAuthProviders(ModelBuilder builder)
    {
        builder.Entity<IdentityProviderEntity>(entity =>
        {
            entity.ToTable("eiam_identity_provider");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Code).HasColumnName("code_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.ProviderType).HasColumnName("provider_type").HasConversion<int>();
            entity.Property(p => p.Enabled).HasColumnName("enabled_");
            entity.Property(p => p.ConfigJson).HasColumnName("config_").HasColumnType("jsonb").IsRequired();
            entity.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<IdentitySourceEntity>(entity =>
        {
            entity.ToTable("eiam_identity_source");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Code).HasColumnName("code_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.ProviderType).HasColumnName("provider_type").HasConversion<int>();
            entity.Property(p => p.Enabled).HasColumnName("enabled_");
            entity.Property(p => p.BasicConfigJson).HasColumnName("basic_config").HasColumnType("jsonb").IsRequired();
            entity.Property(p => p.StrategyConfigJson).HasColumnName("strategy_config").HasColumnType("jsonb").IsRequired();
            entity.Property(p => p.JobConfigJson).HasColumnName("job_config").HasColumnType("jsonb").IsRequired();
            entity.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureAuthBindings(ModelBuilder builder)
    {
        builder.Entity<ThirdPartyUserEntity>(entity =>
        {
            entity.ToTable("eiam_third_party_user");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.IdentityProviderId).HasColumnName("identity_provider_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.OpenId).HasColumnName("open_id").HasMaxLength(128).IsRequired();
            entity.Property(p => p.UnionId).HasColumnName("union_id").HasMaxLength(128);
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128);
            entity.Property(p => p.Email).HasColumnName("email_").HasMaxLength(256);
            entity.Property(p => p.Mobile).HasColumnName("mobile_").HasMaxLength(64);
            entity.Property(p => p.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(1024);
            entity.Property(p => p.RawProfileJson).HasColumnName("raw_profile").HasColumnType("jsonb").IsRequired();
            entity.Property(p => p.LastLoginTime).HasColumnName("last_login_time");
            entity.HasIndex(p => new { p.TenantId, p.IdentityProviderId, p.OpenId }).IsUnique();
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<UserIdpBindEntity>(entity =>
        {
            entity.ToTable("eiam_user_idp_bind");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.ThirdPartyUserId).HasColumnName("third_party_user_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.BoundTime).HasColumnName("bound_time");
            entity.HasIndex(p => new { p.TenantId, p.UserId, p.ThirdPartyUserId }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureApplications(ModelBuilder builder)
    {
        builder.Entity<AppEntity>(entity =>
        {
            entity.ToTable("eiam_app");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Code).HasColumnName("code_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Protocol).HasColumnName("protocol_").HasMaxLength(32).IsRequired();
            entity.Property(p => p.Enabled).HasColumnName("enabled_");
            entity.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<AppAccessPolicyEntity>(entity =>
        {
            entity.ToTable("eiam_app_access_policy");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.AppId).HasColumnName("app_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.SubjectType).HasColumnName("subject_type").HasConversion<int>();
            entity.Property(p => p.SubjectId).HasColumnName("subject_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.AllowAccess).HasColumnName("allow_access");
            entity.HasIndex(p => new { p.TenantId, p.AppId, p.SubjectType, p.SubjectId }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureSync(ModelBuilder builder)
    {
        builder.Entity<IdentitySourceSyncHistoryEntity>(entity =>
        {
            entity.ToTable("eiam_identity_source_sync_history");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.IdentitySourceId).HasColumnName("identity_source_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.TriggerMode).HasColumnName("trigger_mode").HasMaxLength(32).IsRequired();
            entity.Property(p => p.Status).HasColumnName("status_").HasConversion<int>();
            entity.Property(p => p.TotalUsers).HasColumnName("total_users");
            entity.Property(p => p.CreatedUsers).HasColumnName("created_users");
            entity.Property(p => p.UpdatedUsers).HasColumnName("updated_users");
            entity.Property(p => p.DeletedUsers).HasColumnName("deleted_users");
            entity.Property(p => p.SkippedUsers).HasColumnName("skipped_users");
            entity.Property(p => p.ErrorMessage).HasColumnName("error_message").HasMaxLength(2048);
            entity.Property(p => p.StartedTime).HasColumnName("started_time");
            entity.Property(p => p.EndedTime).HasColumnName("ended_time");
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<IdentitySourceSyncRecordEntity>(entity =>
        {
            entity.ToTable("eiam_identity_source_sync_record");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.SyncHistoryId).HasColumnName("sync_history_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.ObjectType).HasColumnName("object_type").HasMaxLength(64).IsRequired();
            entity.Property(p => p.ObjectId).HasColumnName("object_id").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Action).HasColumnName("action_").HasMaxLength(32).IsRequired();
            entity.Property(p => p.Result).HasColumnName("result_").HasMaxLength(32).IsRequired();
            entity.Property(p => p.Detail).HasColumnName("detail_").HasColumnType("text");
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureRbac(ModelBuilder builder)
    {
        builder.Entity<PermissionEntity>(entity =>
        {
            entity.ToTable("eiam_permission");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Code).HasColumnName("code_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Resource).HasColumnName("resource_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Action).HasColumnName("action_").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Description).HasColumnName("description_").HasMaxLength(512);
            entity.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<RolePermissionEntity>(entity =>
        {
            entity.ToTable("eiam_role_permission");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.RoleId).HasColumnName("role_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.PermissionId).HasColumnName("permission_id").HasMaxLength(64).IsRequired();
            entity.HasIndex(p => new { p.TenantId, p.RoleId, p.PermissionId }).IsUnique();
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<UserPermissionGrantEntity>(entity =>
        {
            entity.ToTable("eiam_user_permission_grant");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.PermissionId).HasColumnName("permission_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Effect).HasColumnName("effect_").HasConversion<int>();
            entity.HasIndex(p => new { p.TenantId, p.UserId, p.PermissionId, p.Effect }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureAudit(ModelBuilder builder)
    {
        builder.Entity<AuditEventEntity>(entity =>
        {
            entity.ToTable("eiam_audit");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.ActorId).HasColumnName("actor_id").HasMaxLength(64);
            entity.Property(p => p.ActorType).HasColumnName("actor_type").HasMaxLength(32).IsRequired();
            entity.Property(p => p.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Content).HasColumnName("content_").HasMaxLength(2048).IsRequired();
            entity.Property(p => p.RequestId).HasColumnName("request_id").HasMaxLength(64);
            entity.Property(p => p.SessionId).HasColumnName("session_id").HasMaxLength(64);
            entity.Property(p => p.TargetJson).HasColumnName("target_").HasColumnType("jsonb");
            entity.Property(p => p.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            entity.Property(p => p.IpAddress).HasColumnName("ip_address").HasMaxLength(128);
            entity.Property(p => p.GeoLocation).HasColumnName("geo_location").HasMaxLength(256);
            entity.Property(p => p.ResultStatus).HasColumnName("result_status").HasMaxLength(32).IsRequired();
            entity.Property(p => p.OccurredTime).HasColumnName("occurred_time");
            entity.HasIndex(p => new { p.TenantId, p.OccurredTime });
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureSamlScim(ModelBuilder builder)
    {
        builder.Entity<SamlServiceProviderEntity>(entity =>
        {
            entity.ToTable("eiam_saml_service_provider");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Code).HasColumnName("code_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.EntityId).HasColumnName("entity_id").HasMaxLength(512).IsRequired();
            entity.Property(p => p.AssertionConsumerServiceUrl).HasColumnName("acs_url").HasMaxLength(1024).IsRequired();
            entity.Property(p => p.SingleLogoutServiceUrl).HasColumnName("slo_url").HasMaxLength(1024);
            entity.Property(p => p.NameIdFormat).HasColumnName("name_id_format").HasMaxLength(256).IsRequired();
            entity.Property(p => p.Audience).HasColumnName("audience_").HasMaxLength(512);
            entity.Property(p => p.RelayStateDefault).HasColumnName("relay_state_default").HasMaxLength(512);
            entity.Property(p => p.WantSignedAssertions).HasColumnName("want_signed_assertions");
            entity.Property(p => p.AllowUnsolicitedResponse).HasColumnName("allow_unsolicited_response");
            entity.Property(p => p.BindingType).HasColumnName("binding_type").HasConversion<int>();
            entity.Property(p => p.SigningCertificatePem).HasColumnName("signing_cert_pem").HasColumnType("text");
            entity.Property(p => p.Enabled).HasColumnName("enabled_");
            entity.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            entity.HasIndex(p => new { p.TenantId, p.EntityId }).IsUnique();
            ConfigureAuditedColumns(entity);
        });

        builder.Entity<ScimAccessTokenEntity>(entity =>
        {
            entity.ToTable("eiam_scim_access_token");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name_").HasMaxLength(128).IsRequired();
            entity.Property(p => p.TokenHash).HasColumnName("token_hash").HasMaxLength(256).IsRequired();
            entity.Property(p => p.ExpiresTime).HasColumnName("expires_time");
            entity.Property(p => p.LastUsedTime).HasColumnName("last_used_time");
            entity.Property(p => p.IsActive).HasColumnName("is_active");
            entity.HasIndex(p => p.TokenHash).IsUnique();
            entity.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureSystemSettings(ModelBuilder builder)
    {
        builder.Entity<SystemSettingEntity>(entity =>
        {
            entity.ToTable("eiam_system_setting");
            entity.Property(p => p.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            entity.Property(p => p.SettingKey).HasColumnName("setting_key").HasMaxLength(128).IsRequired();
            entity.Property(p => p.ValueJson).HasColumnName("value_json").HasColumnType("jsonb").IsRequired();
            entity.HasIndex(p => new { p.TenantId, p.SettingKey }).IsUnique();
            ConfigureAuditedColumns(entity);
        });
    }

    private static void ConfigureAuditedColumns<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : AuditedEntityBase
    {
        entity.Property(p => p.Id).HasColumnName("id_").HasMaxLength(64).IsRequired();
        entity.Property(p => p.CreateBy).HasColumnName("create_by").HasMaxLength(64);
        entity.Property(p => p.CreateTime).HasColumnName("create_time");
        entity.Property(p => p.UpdateBy).HasColumnName("update_by").HasMaxLength(64);
        entity.Property(p => p.UpdateTime).HasColumnName("update_time");
        entity.Property(p => p.IsDeleted).HasColumnName("is_deleted");
        entity.Property(p => p.Remark).HasColumnName("remark_").HasMaxLength(512);
    }
}
