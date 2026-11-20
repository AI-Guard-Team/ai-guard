using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Y2_AgentSessionKeyHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "session_key_hash",
                table: "agent_pairings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "session_key_hash",
                table: "agent_pairings");
        }
    }
}
