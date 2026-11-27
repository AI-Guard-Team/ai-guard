# Yetkisi kısıtlı veritabanı kullanıcısı (S-7)

Yerel geliştirmede ve CI'da uygulama, tabloların sahibi olan kullanıcıyla (`postgres` ya da `.env`'deki `POSTGRES_USER`) bağlanıyor. Bu kolay, ama uygulama ele geçirilirse saldırgan tablo silebilir, sütun değiştirebilir, `REVOKE` edilen yetkileri geri verebilir. Sınav gerçek adaylarla yapılacaksa iki kullanıcı ayrılmalı (GAB 3.3.2, "veritabanı ve servis hesapları yalnızca gerekli izinlere sahip olmalı"):

| Kullanıcı | Ne için | Yetki |
|---|---|---|
| Sahip (ör. `aiguard`) | Göçler (`migrate` servisi, `dotnet ef database update`) | Tabloların sahibi |
| Uygulama (ör. `aiguard_app`) | API'nin bağlantı dizesi | Yalnızca veri okuma ve yazma; tablo oluşturamaz, silemez, değiştiremez |

Denetim kaydı ve rıza metinleri ayrıca veritabanı tetikleyicileriyle korunuyor (K-32); bu kullanıcı onlara ek bir katman.

## Betik

Göçler uygulandıktan sonra **sahip kullanıcıyla** çalıştırılır. Şifre betiğe yazılmaz, `psql` değişkeniyle verilir:

```bash
psql -h localhost -U aiguard -d aiguard -v app_password="$AIGUARD_APP_PASSWORD" -f docs/sql/uygulama-kullanicisi.sql
```

Betik: [`docs/sql/uygulama-kullanicisi.sql`](sql/uygulama-kullanicisi.sql). Yaptıkları:

- `aiguard_app` adında giriş yapabilen bir kullanıcı oluşturur (varsa şifresini günceller).
- Veritabanına bağlanma ve `public` şemasını kullanma izni verir; şemada nesne oluşturma izni vermez.
- Bütün tablolarda `SELECT, INSERT, UPDATE, DELETE`; dizilerde `USAGE, SELECT`.
- Sahibin ileride göçle açacağı tablolar için aynı yetkileri varsayılan yapar (her göçten sonra betiği yeniden çalıştırmak gerekmesin).
- `audit_logs`'ta `UPDATE` yetkisini geri alır; silme yalnızca 1 yıldan eski kayıtta (saklama görevi), tetikleyici zaten zorluyor.
- `consent_texts`'te `UPDATE` ve `DELETE` yetkisini geri alır; yeni sürüm yalnızca eklenir.
- `__EFMigrationsHistory`'de yalnızca okuma bırakır; göç geçmişini uygulama değiştiremez.

## Uygulamayı bu kullanıcıyla çalıştırmak

- **Docker:** Kendiliğinden. `migrate` sahip kullanıcıyla göçleri uygular, ardından tek seferlik `db-user` servisi bu betiği çalıştırır, `api` `aiguard_app` ile bağlanır. Şifre `.env`'deki `APP_DB_PASSWORD`.
- **Docker'sız:** API'nin user-secrets'ındaki bağlantı dizesini uygulama kullanıcısıyla değiştirin. Göç için `dotnet ef database update --connection "<sahip bağlantı dizesi>"`.
- **Testler:** Entegrasyon testleri her sınıf için veritabanı oluşturup sildiği için sahip kullanıcıyla çalışmaya devam eder (K-09).

## Denendi

Betik, yerel PostgreSQL 16'da göçleri uygulanmış boş bir veritabanında denendi:

- Uygulama kullanıcısı tabloları okuyabiliyor, `audit_logs`'a ekleme yapabiliyor.
- `CREATE TABLE`, `DROP TABLE`, `ALTER TABLE`, `audit_logs` güncellemesi, `consent_texts` güncellemesi ve göç geçmişini silme reddediliyor.
- Betik ikinci kez çalıştırılınca hata vermiyor (şifreyi güncelliyor); şifre çıktıya yazılmıyor.

API de bu kullanıcıyla (Development, örnek veriyle) çalıştırıldı: örnek veri yüklendi; giriş, sınav listesi, sınav oluşturma/düzenleme/silme, soru ekleme, rıza metni çalıştı; yetkisiz istek 403 aldı ve denetim kaydına yazıldı. Aynı API üzerinde sınav ekranı sahte kamerayla da denendi (K-50, S-29).

Denenmeyenler: saklama görevinin gerçek silmesi (günde bir çalışıyor; yetkisi `DELETE` olarak verili, `audit_logs`'ta tetikleyici yalnızca 1 yıldan eski kaydı kabul ediyor) ve Docker'daki `db-user` adımı (bu bilgisayarda Docker yok; CI'daki compose işi çalıştırıyor).
