using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // S-16 (A5): ajan oturum anahtarı veritabanında yalnızca özetiyle tutuluyor (K-21); şifreli anahtar sütunu
    // hiç doldurulmadı, kaldırılması veri kaybı değil.
    public partial class A5_AjanAnahtariSutunuKaldirildi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encrypted_session_key",
                table: "agent_pairings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "encrypted_session_key",
                table: "agent_pairings",
                type: "bytea",
                nullable: true);
        }
    }
}
