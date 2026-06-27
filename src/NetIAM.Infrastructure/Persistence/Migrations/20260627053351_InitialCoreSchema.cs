using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NetIAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eiam_app",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    protocol_ = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    enabled_ = table.Column<bool>(type: "boolean", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_app", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_app_access_policy",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    app_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_type = table.Column<int>(type: "integer", nullable: false),
                    subject_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    allow_access = table.Column<bool>(type: "boolean", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_app_access_policy", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_audit",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    actor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    content_ = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    target_ = table.Column<string>(type: "jsonb", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    geo_location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    result_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occurred_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_audit", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_identity_provider",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    enabled_ = table.Column<bool>(type: "boolean", nullable: false),
                    config_ = table.Column<string>(type: "jsonb", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_identity_provider", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_identity_source",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    enabled_ = table.Column<bool>(type: "boolean", nullable: false),
                    basic_config = table.Column<string>(type: "jsonb", nullable: false),
                    strategy_config = table.Column<string>(type: "jsonb", nullable: false),
                    job_config = table.Column<string>(type: "jsonb", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_identity_source", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_identity_source_sync_history",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    identity_source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    trigger_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status_ = table.Column<int>(type: "integer", nullable: false),
                    total_users = table.Column<int>(type: "integer", nullable: false),
                    created_users = table.Column<int>(type: "integer", nullable: false),
                    updated_users = table.Column<int>(type: "integer", nullable: false),
                    deleted_users = table.Column<int>(type: "integer", nullable: false),
                    skipped_users = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    started_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_identity_source_sync_history", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_identity_source_sync_record",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sync_history_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    action_ = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    result_ = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detail_ = table.Column<string>(type: "text", nullable: true),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_identity_source_sync_record", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_oidc_application",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_oidc_application", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "eiam_oidc_scope",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_oidc_scope", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "eiam_organization",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    code_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    parent_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    path_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    display_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    identity_source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    data_origin = table.Column<int>(type: "integer", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_organization", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_organization_member",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    organization_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_organization_member", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_role",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "text", nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_role", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_tenant",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    identifier_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    default_domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_tenant", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_third_party_user",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    identity_provider_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    open_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    union_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    email_ = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    mobile_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    raw_profile = table.Column<string>(type: "jsonb", nullable: false),
                    last_login_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_third_party_user", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    data_origin = table.Column<int>(type: "integer", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    username_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    email_ = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    phone_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_group",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_group", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_group_member",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_group_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_group_member", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_idp_bind",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    third_party_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bound_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_idp_bind", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_oidc_authorization",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_oidc_authorization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eiam_oidc_authorization_eiam_oidc_application_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "eiam_oidc_application",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "eiam_role_claim",
                columns: table => new
                {
                    id_ = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_role_claim", x => x.id_);
                    table.ForeignKey(
                        name: "FK_eiam_role_claim_eiam_role_role_id",
                        column: x => x.role_id,
                        principalTable: "eiam_role",
                        principalColumn: "id_",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_claim",
                columns: table => new
                {
                    id_ = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_claim", x => x.id_);
                    table.ForeignKey(
                        name: "FK_eiam_user_claim_eiam_user_user_id",
                        column: x => x.user_id,
                        principalTable: "eiam_user",
                        principalColumn: "id_",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_login",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<string>(type: "character varying(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_login", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_eiam_user_login_eiam_user_user_id",
                        column: x => x.user_id,
                        principalTable: "eiam_user",
                        principalColumn: "id_",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_role",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    role_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_role", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_eiam_user_role_eiam_role_role_id",
                        column: x => x.role_id,
                        principalTable: "eiam_role",
                        principalColumn: "id_",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eiam_user_role_eiam_user_user_id",
                        column: x => x.user_id,
                        principalTable: "eiam_user",
                        principalColumn: "id_",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_token",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name_ = table.Column<string>(type: "text", nullable: false),
                    value_ = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_token", x => new { x.user_id, x.login_provider, x.name_ });
                    table.ForeignKey(
                        name: "FK_eiam_user_token_eiam_user_user_id",
                        column: x => x.user_id,
                        principalTable: "eiam_user",
                        principalColumn: "id_",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eiam_oidc_token",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    AuthorizationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_oidc_token", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eiam_oidc_token_eiam_oidc_application_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "eiam_oidc_application",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_eiam_oidc_token_eiam_oidc_authorization_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalTable: "eiam_oidc_authorization",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_app_tenant_id_code_",
                table: "eiam_app",
                columns: new[] { "tenant_id", "code_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_app_access_policy_tenant_id_app_id_subject_type_subjec~",
                table: "eiam_app_access_policy",
                columns: new[] { "tenant_id", "app_id", "subject_type", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_audit_tenant_id_occurred_time",
                table: "eiam_audit",
                columns: new[] { "tenant_id", "occurred_time" });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_identity_provider_tenant_id_code_",
                table: "eiam_identity_provider",
                columns: new[] { "tenant_id", "code_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_identity_source_tenant_id_code_",
                table: "eiam_identity_source",
                columns: new[] { "tenant_id", "code_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_oidc_application_ClientId",
                table: "eiam_oidc_application",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_oidc_authorization_ApplicationId_Status_Subject_Type",
                table: "eiam_oidc_authorization",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_oidc_scope_Name",
                table: "eiam_oidc_scope",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_oidc_token_ApplicationId_Status_Subject_Type",
                table: "eiam_oidc_token",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_oidc_token_AuthorizationId",
                table: "eiam_oidc_token",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_eiam_oidc_token_ReferenceId",
                table: "eiam_oidc_token",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_organization_tenant_id_code_",
                table: "eiam_organization",
                columns: new[] { "tenant_id", "code_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_organization_tenant_id_external_id",
                table: "eiam_organization",
                columns: new[] { "tenant_id", "external_id" });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_organization_member_tenant_id_organization_id_user_id",
                table: "eiam_organization_member",
                columns: new[] { "tenant_id", "organization_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "eiam_role",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_role_claim_role_id",
                table: "eiam_role_claim",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_eiam_tenant_identifier_",
                table: "eiam_tenant",
                column: "identifier_",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_third_party_user_tenant_id_identity_provider_id_open_id",
                table: "eiam_third_party_user",
                columns: new[] { "tenant_id", "identity_provider_id", "open_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "eiam_user",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_tenant_id_external_id",
                table: "eiam_user",
                columns: new[] { "tenant_id", "external_id" });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_tenant_id_username_",
                table: "eiam_user",
                columns: new[] { "tenant_id", "username_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "eiam_user",
                column: "normalized_username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_claim_user_id",
                table: "eiam_user_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_group_tenant_id_name_",
                table: "eiam_user_group",
                columns: new[] { "tenant_id", "name_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_group_member_tenant_id_user_group_id_user_id",
                table: "eiam_user_group_member",
                columns: new[] { "tenant_id", "user_group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_idp_bind_tenant_id_user_id_third_party_user_id",
                table: "eiam_user_idp_bind",
                columns: new[] { "tenant_id", "user_id", "third_party_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_login_user_id",
                table: "eiam_user_login",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_role_role_id",
                table: "eiam_user_role",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eiam_app");

            migrationBuilder.DropTable(
                name: "eiam_app_access_policy");

            migrationBuilder.DropTable(
                name: "eiam_audit");

            migrationBuilder.DropTable(
                name: "eiam_identity_provider");

            migrationBuilder.DropTable(
                name: "eiam_identity_source");

            migrationBuilder.DropTable(
                name: "eiam_identity_source_sync_history");

            migrationBuilder.DropTable(
                name: "eiam_identity_source_sync_record");

            migrationBuilder.DropTable(
                name: "eiam_oidc_scope");

            migrationBuilder.DropTable(
                name: "eiam_oidc_token");

            migrationBuilder.DropTable(
                name: "eiam_organization");

            migrationBuilder.DropTable(
                name: "eiam_organization_member");

            migrationBuilder.DropTable(
                name: "eiam_role_claim");

            migrationBuilder.DropTable(
                name: "eiam_tenant");

            migrationBuilder.DropTable(
                name: "eiam_third_party_user");

            migrationBuilder.DropTable(
                name: "eiam_user_claim");

            migrationBuilder.DropTable(
                name: "eiam_user_group");

            migrationBuilder.DropTable(
                name: "eiam_user_group_member");

            migrationBuilder.DropTable(
                name: "eiam_user_idp_bind");

            migrationBuilder.DropTable(
                name: "eiam_user_login");

            migrationBuilder.DropTable(
                name: "eiam_user_role");

            migrationBuilder.DropTable(
                name: "eiam_user_token");

            migrationBuilder.DropTable(
                name: "eiam_oidc_authorization");

            migrationBuilder.DropTable(
                name: "eiam_role");

            migrationBuilder.DropTable(
                name: "eiam_user");

            migrationBuilder.DropTable(
                name: "eiam_oidc_application");
        }
    }
}
