using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetIAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2SamlScimRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eiam_permission",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    resource_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    action_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_eiam_permission", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_role_permission",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    role_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    permission_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_role_permission", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_saml_service_provider",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    acs_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    slo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    name_id_format = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    audience_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    relay_state_default = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    want_signed_assertions = table.Column<bool>(type: "boolean", nullable: false),
                    allow_unsolicited_response = table.Column<bool>(type: "boolean", nullable: false),
                    binding_type = table.Column<int>(type: "integer", nullable: false),
                    signing_cert_pem = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_eiam_saml_service_provider", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_scim_access_token",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_ = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expires_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_scim_access_token", x => x.id_);
                });

            migrationBuilder.CreateTable(
                name: "eiam_user_permission_grant",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    permission_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    effect_ = table.Column<int>(type: "integer", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_user_permission_grant", x => x.id_);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_permission_tenant_id_code_",
                table: "eiam_permission",
                columns: new[] { "tenant_id", "code_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_role_permission_tenant_id_role_id_permission_id",
                table: "eiam_role_permission",
                columns: new[] { "tenant_id", "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_saml_service_provider_tenant_id_code_",
                table: "eiam_saml_service_provider",
                columns: new[] { "tenant_id", "code_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_saml_service_provider_tenant_id_entity_id",
                table: "eiam_saml_service_provider",
                columns: new[] { "tenant_id", "entity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_scim_access_token_tenant_id_name_",
                table: "eiam_scim_access_token",
                columns: new[] { "tenant_id", "name_" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_scim_access_token_token_hash",
                table: "eiam_scim_access_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiam_user_permission_grant_tenant_id_user_id_permission_id_~",
                table: "eiam_user_permission_grant",
                columns: new[] { "tenant_id", "user_id", "permission_id", "effect_" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eiam_permission");

            migrationBuilder.DropTable(
                name: "eiam_role_permission");

            migrationBuilder.DropTable(
                name: "eiam_saml_service_provider");

            migrationBuilder.DropTable(
                name: "eiam_scim_access_token");

            migrationBuilder.DropTable(
                name: "eiam_user_permission_grant");
        }
    }
}
