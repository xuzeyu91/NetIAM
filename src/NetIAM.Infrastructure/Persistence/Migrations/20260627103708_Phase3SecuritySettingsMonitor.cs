using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetIAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3SecuritySettingsMonitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eiam_system_setting",
                columns: table => new
                {
                    id_ = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    setting_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    create_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    create_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    update_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    update_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remark_ = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiam_system_setting", x => x.id_);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eiam_system_setting_tenant_id_setting_key",
                table: "eiam_system_setting",
                columns: new[] { "tenant_id", "setting_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eiam_system_setting");
        }
    }
}
