using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UI1_YonetimAyarlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invite_sent_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_login_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "risk_session_threshold",
                table: "exams",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alternative_method_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    support_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    support_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    agent_download_url_windows = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    agent_download_url_mac = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropColumn(
                name: "invite_sent_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "risk_session_threshold",
                table: "exams");
        }
    }
}
