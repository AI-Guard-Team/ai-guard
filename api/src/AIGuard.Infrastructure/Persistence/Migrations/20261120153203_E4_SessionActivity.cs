using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class E4_SessionActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disconnected_at",
                table: "exam_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_seen_at",
                table: "exam_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disconnected_at",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "last_seen_at",
                table: "exam_sessions");
        }
    }
}
