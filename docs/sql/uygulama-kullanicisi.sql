-- Yetkisi kısıtlı uygulama kullanıcısı (S-7, docs/veritabani-kullanicisi.md).
-- Göçler uygulandıktan sonra tabloların SAHİBİ olan kullanıcıyla çalıştırılır:
--   psql -h localhost -U aiguard -d aiguard -v app_password="$AIGUARD_APP_PASSWORD" -f docs/sql/uygulama-kullanicisi.sql
-- Şifre buraya yazılmaz; psql değişkeniyle verilir.

\set ON_ERROR_STOP on

-- Kullanıcıyı oluştur ya da şifresini güncelle. \gexec komutu çalıştırır ama metnini yazdırmaz; şifre çıktıya düşmez.
SELECT format('CREATE ROLE aiguard_app LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aiguard_app')
UNION ALL
SELECT format('ALTER ROLE aiguard_app LOGIN PASSWORD %L', :'app_password')
WHERE EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aiguard_app')
\gexec

-- Bağlanma ve şemayı kullanma; şemada nesne oluşturma yok.
DO $$ BEGIN EXECUTE format('GRANT CONNECT ON DATABASE %I TO aiguard_app', current_database()); END $$;
GRANT USAGE ON SCHEMA public TO aiguard_app;
REVOKE CREATE ON SCHEMA public FROM aiguard_app;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- Veri okuma ve yazma.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO aiguard_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO aiguard_app;

-- Sahibin ileride göçle açacağı tablolar için de aynı yetkiler.
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO aiguard_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO aiguard_app;

-- Değiştirilemez kayıtlar (K-10, K-32). Tetikleyiciler zaten durduruyor; yetki de yok.
-- audit_logs: güncelleme yok; silme yalnızca 1 yıldan eski kayıtta (saklama görevi, tetikleyici zorluyor).
REVOKE UPDATE ON audit_logs FROM aiguard_app;
-- consent_texts: yeni sürüm yalnızca eklenir.
REVOKE UPDATE, DELETE ON consent_texts FROM aiguard_app;

-- Göç geçmişini uygulama değiştiremez.
REVOKE INSERT, UPDATE, DELETE ON "__EFMigrationsHistory" FROM aiguard_app;
