using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S4_PermissionRevokedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "permission_revoked_at",
                table: "exam_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "permission_revoked_at",
                table: "exam_sessions");
        }
    }
}
