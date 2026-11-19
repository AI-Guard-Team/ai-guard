using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GabUyumu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_exam_sessions_exam_id_candidate_id",
                table: "exam_sessions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "attempt_used_at",
                table: "exam_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform",
                table: "agent_pairings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_exam_id_candidate_id",
                table: "exam_sessions",
                columns: new[] { "exam_id", "candidate_id" },
                unique: true,
                filter: "state <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_exam_sessions_exam_id_candidate_id",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "attempt_used_at",
                table: "exam_assignments");

            migrationBuilder.DropColumn(
                name: "platform",
                table: "agent_pairings");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_exam_id_candidate_id",
                table: "exam_sessions",
                columns: new[] { "exam_id", "candidate_id" },
                unique: true);
        }
    }
}
