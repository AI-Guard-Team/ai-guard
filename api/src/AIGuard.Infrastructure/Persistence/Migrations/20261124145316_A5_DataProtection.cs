using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AIGuard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class A5_DataProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "monitoring_data_deleted_at",
                table: "exam_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "consent_texts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_texts", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "consent_texts",
                columns: new[] { "id", "body", "published_at", "title", "type", "version" },
                values: new object[,]
                {
                    { new Guid("6f1e2d0a-5b7c-4c39-9a51-0c2f6d8e1a01"), "Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu (KVKK) kapsamında kayıt fotoğrafınızdan elde edilen biyometrik verinin nasıl işlendiğini açıklar.\n\nİşlenen veri: Yüklediğiniz kayıt fotoğrafından bir yüz vektörü (sayısal yüz şablonu) çıkarılır. Fotoğrafın kendisi saklanmaz; vektör çıkarıldıktan hemen sonra silinir. Yüz vektörü KVKK'ya göre özel nitelikli kişisel veridir.\n\nAmaç: Yüz vektörü yalnızca sınav başında ve sınav sırasında kimliğinizi doğrulamak, yani sınava sizin girdiğinizi kontrol etmek için kullanılır.\n\nHukuki sebep: Açık rızanız (KVKK madde 6).\n\nSaklama: Yüz vektörü şifreli olarak saklanır. Son sınavınızdan 30 gün sonra ya da rızanızı geri aldığınızda silinir.\n\nErişim: Yüz vektörünü hiçbir kullanıcı göremez ve indiremez; yalnızca sistem kimlik eşleştirmesi için kullanır.\n\nRızanızı geri alma: Rızanızı profil sayfanızdan istediğiniz zaman geri alabilirsiniz. Bu durumda yüz vektörünüz silinir ve süren sınav oturumunuz iptal edilir. Rıza vermemeniz ya da geri almanız durumunda sınavı biyometrik olmayan alternatif bir yöntemle yapmak için sınavı düzenleyen birime başvurabilirsiniz.\n\nHaklarınız: KVKK madde 11 kapsamındaki haklarınızı (bilgi talep etme, düzeltme, silme vb.) kullanmak için sınavı düzenleyen birime başvurabilirsiniz.\n\nYukarıdaki bilgileri okudum. Kayıt fotoğrafımdan elde edilecek yüz vektörünün kimlik doğrulama amacıyla işlenmesine açık rıza veriyorum.", new DateTimeOffset(new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Biyometrik veri aydınlatma metni ve açık rıza", "Biometric", "kvkk-biometric-v1" },
                    { new Guid("6f1e2d0a-5b7c-4c39-9a51-0c2f6d8e1a02"), "Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu (KVKK) kapsamında sınav sırasında hangi verilerin işlendiğini açıklar.\n\nİşlenen veriler:\n- Kamera görüntüsü ve mikrofon sesi sınav sırasında anlık olarak analiz edilir (yüz, bakış yönü, telefon, konuşma). Sürekli video veya ses kaydı tutulmaz; analiz edilen kareler ve ses parçaları analizden hemen sonra silinir.\n- Kural dışı bir durum tespit edildiğinde (olay) o ana ait kamera karesi kanıt olarak şifreli saklanır.\n- Tarayıcı olayları (sekme değiştirme, tam ekrandan çıkma, kopyala-yapıştır), güvenlik ajanının bulduğu göstergeler (sanal makine, yasaklı uygulama, VPN) ve bağlantı bilgileri kaydedilir.\n- Sınav başında ve sınav sırasında yüz doğrulaması yapılır.\n\nAmaç: Sınavın kurallara uygun yapıldığını doğrulamak. Sistem kopya kararı vermez; tespit edilen olaylar ve risk skoru gözetmenin değerlendirmesine sunulur, nihai karar gözetmene aittir.\n\nHukuki sebep: Açık rızanız (KVKK madde 6).\n\nSaklama: Kanıt kareleri, olaylar ve cihaz göstergeleri sınavın saklama süresi (varsayılan 30 gün) sonunda otomatik olarak silinir. Sınav sonucunuz ve gözetmen kararı saklanmaya devam eder. Rıza ve denetim kayıtları ispat amacıyla 1 yıl saklanır.\n\nErişim: Kanıt karelerine ve olaylara yalnızca sınava atanmış gözetmen ve yönetici erişebilir.\n\nRıza vermezseniz: Sınav başlamaz ve sınav hakkınız kullanılmış sayılmaz. Sınavı alternatif bir yöntemle yapmak için sınavı düzenleyen birime başvurabilirsiniz.\n\nHaklarınız: KVKK madde 11 kapsamındaki haklarınızı kullanmak için sınavı düzenleyen birime başvurabilirsiniz.\n\nYukarıdaki bilgileri okudum. Bu sınav oturumu süresince kamera, mikrofon ve cihaz verilerimin yukarıdaki amaçla işlenmesine açık rıza veriyorum.", new DateTimeOffset(new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sınav izleme aydınlatma metni ve açık rıza", "Session", "kvkk-session-v1" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_consent_texts_type_published_at",
                table: "consent_texts",
                columns: new[] { "type", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_consent_texts_version",
                table: "consent_texts",
                column: "version",
                unique: true);

            // K-10'un ikinci adımı (K-32): denetim kaydı ve rıza metinleri veritabanında da yalnızca eklemeli.
            // Uygulama tabloların sahibiyle bağlandığı için REVOKE yetmiyor; tetikleyici sahibi de durduruyor.
            // ExecuteUpdate/ExecuteDelete ve ham SQL interceptor'a uğramadığı için bu koruma gerekli.
            migrationBuilder.Sql("""
                CREATE FUNCTION reject_modification() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION '% tablosu yalnızca eklemeli; % reddedildi (K-32)', TG_TABLE_NAME, TG_OP;
                END $$;

                -- Tek istisna: saklama görevinin 1 yılı dolan denetim kayıtlarını silmesi (GAB 3.3.8).
                CREATE FUNCTION audit_logs_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' AND OLD.occurred_at < now() - interval '1 year' THEN
                        RETURN OLD;
                    END IF;
                    RAISE EXCEPTION 'audit_logs tablosu yalnızca eklemeli; % reddedildi (K-32)', TG_OP;
                END $$;

                CREATE TRIGGER trg_audit_logs_append_only BEFORE UPDATE OR DELETE ON audit_logs
                    FOR EACH ROW EXECUTE FUNCTION audit_logs_append_only();
                CREATE TRIGGER trg_audit_logs_no_truncate BEFORE TRUNCATE ON audit_logs
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_modification();

                CREATE TRIGGER trg_consent_texts_append_only BEFORE UPDATE OR DELETE ON consent_texts
                    FOR EACH ROW EXECUTE FUNCTION reject_modification();
                CREATE TRIGGER trg_consent_texts_no_truncate BEFORE TRUNCATE ON consent_texts
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_modification();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_logs_append_only ON audit_logs;
                DROP TRIGGER IF EXISTS trg_audit_logs_no_truncate ON audit_logs;
                DROP FUNCTION IF EXISTS audit_logs_append_only();
                """);

            migrationBuilder.DropTable(
                name: "consent_texts");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS reject_modification();");

            migrationBuilder.DropColumn(
                name: "monitoring_data_deleted_at",
                table: "exam_sessions");
        }
    }
}
