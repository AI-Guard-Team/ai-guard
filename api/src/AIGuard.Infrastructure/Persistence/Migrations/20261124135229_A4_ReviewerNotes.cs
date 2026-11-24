using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class A4_ReviewerNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reviewer_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviewer_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_reviewer_notes_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reviewer_notes_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_notes_author_id",
                table: "reviewer_notes",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_notes_exam_session_id_created_at",
                table: "reviewer_notes",
                columns: new[] { "exam_session_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviewer_notes");
        }
    }
}
