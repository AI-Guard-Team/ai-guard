# S4 · Tarayıcı deneme notları (kamera, ses, izin ve olaylar)

Otomatik testler jsdom'da çalışıyor. Orada gerçek kamera, `MediaRecorder` ve tam ekran yok. Bu deneme gerçek tarayıcıda yapılıyor; sonuçlar aşağıdaki tabloya işleniyor. Kararlar `MIMARI-KARARLAR.md` K-30'da.

## Hazırlık

1. Güncel kodla ortamı kur:
   ```powershell
   docker compose up -d --build
   ```
   Yeni göç `S4_PermissionRevokedAt` compose'daki `migrate` servisiyle uygulanıyor. Compose'suz çalışıyorsan önce `dotnet ef database update` çalıştır.
2. Örnek veriyle bir aday hesabıyla gir, sınavı başlat, rıza ve ajan adımlarını geç, kimliği doğrula.
3. Geliştirici araçlarında **Network** sekmesini aç ve `frames`, `audio`, `events`, `permission` için filtrele.
4. Veritabanındaki olayları izlemek için:
   ```sql
   select type, source, started_at, duration_sec from events where exam_session_id = '<oturum>' order by received_at;
   select state, flags, permission_revoked_at from exam_sessions where id = '<oturum>';
   ```

## Adımlar

| # | Adım | Beklenen |
|---|---|---|
| 1 | Sınav ekranı açılır | Tarayıcı kamera ve mikrofon izni ister; izin verilince üst çubukta "Kamera Açık" görünür |
| 2 | 10 sn bekle | Saniyede bir `POST …/frames` 202 alır. `POST …/audio` 202 alır (S-24: WebM kaydedemeyen tarayıcıda ses gitmez) |
| 3 | Başka sekmeye geç, 5 sn sonra dön | Dönüşte `TAB_BLUR` gider; süre ~5 sn |
| 4 | Başka uygulamaya geç (Alt+Tab), 70 sn sonra dön | 60. sn'de bir `TAB_BLUR` (~60 sn), dönüşte bir tane daha (~10 sn) |
| 5 | "Tam ekrana geç" → Esc | `FULLSCREEN_EXIT` |
| 6 | Soru metnini kopyala, yapıştır; sağ tıkla | `COPY_PASTE`, `RIGHT_CLICK`. Arka arkaya hızlı tıklamada saniyede en fazla bir istek gider |
| 7 | Adres çubuğundaki kamera simgesinden kamera iznini kaldır | `permission` `{ granted: false }`, sorular gizlenir, süre işlemeye devam eder, kamera ışığı söner; `frames` durur |
| 8 | 30 sn bekle, "Kamera ve mikrofona izin ver"e bas | İzin istemi çıkar; izin verilince `{ granted: true }`, sorular geri gelir. `events`'te `PERMISSION_REVOKED` ~30 sn, `flags` içinde `PermissionGap` |
| 9 | 7. adımı mikrofon için tekrarla | Aynı akış |
| 10 | İzni kaldırıp sınav süresinin dolmasını bekle | Oturum `Completed`, `PERMISSION_REVOKED` süre sonuna kadar |
| 11 | Ağ bağlantısını 40 sn kes, sonra aç | Oturum `Disconnected`, kamera ve olay gönderimi durur, "Kimliği yeniden doğrula" çıkar; doğrulanınca kareler yeniden başlar |
| 12 | Sınavı teslim et | Kamera ışığı söner, istek gitmez |
| 13 | Uygulama sekmesi (Application) → Local Storage, Session Storage, IndexedDB | Kare ya da ses yok (IndexedDB'de yalnızca cevap kuyruğu) |

## Sonuçlar

**24 Kasım 2026, Windows 11, Chrome ve Edge (otomatik, sahte kamera/mikrofon).** Bilgisayarda kamera olmadığı için tarayıcı `--use-fake-device-for-media-stream` ile çalıştırıldı: görüntü ve ses sentetik, ama `getUserMedia`, `MediaRecorder`, canvas, tam ekran, site izni ve olaylar gerçek tarayıcınınki. İzin kaldırma Chrome'un site izni ayarıyla (`Browser.setPermission`) yapıldı. Betik depoda değil; 34 kontrol, Chrome'da 9, Edge'de 4 çalıştırma.

| Adım | Chrome | Edge | Firefox | Safari |
|---|---|---|---|---|
| 1 (izin verilmeden sorular gizli, süre işliyor) | Geçti | Geçti | Denenmedi | Denenmedi |
| 2 (kare: saniyede 1, JPEG, ~10 KB) | Geçti | Geçti | Denenmedi | Denenmedi |
| 2 (ses: her parça tam WebM, ~3 KB) | Geçti | Geçti | Denenmedi | Denenmedi |
| 3, 4 (`TAB_BLUR`) | **Denenemedi** | **Denenemedi** | Denenmedi | Denenmedi |
| 5 (`FULLSCREEN_EXIT`) | Geçti | Geçti | Denenmedi | Denenmedi |
| 6 (`COPY_PASTE`, `RIGHT_CLICK`) | Geçti | Geçti | Denenmedi | Denenmedi |
| 7 (izin kaldırılınca sorular gizli, süre işliyor, kamera kapalı, kendiliğinden fark ediliyor) | Geçti | Geçti | Denenmedi | Denenmedi |
| 8 (izinle dönüş, `PERMISSION_REVOKED` sunucu süresiyle, `PermissionGap`) | Geçti | Geçti | Denenmedi | Denenmedi |
| 9 (yalnızca mikrofon) | Denenmedi | Denenmedi | Denenmedi | Denenmedi |
| 10 (izin beklerken süre dolması) | Otomatik testte (sunucu) | | | |
| 11 (bağlantı kesintisi) | Denenmedi | Denenmedi | Denenmedi | Denenmedi |
| 12 (teslimde kamera kapanıyor) | Geçti | Geçti | Denenmedi | Denenmedi |
| 13 (tarayıcıda kare/ses saklanmıyor) | Geçti | Geçti | Denenmedi | Denenmedi |
| İzin istemi reddedilince sorular hiç görünmüyor | Geçti | Geçti | Denenmedi | Denenmedi |

**Denemede bulunup düzeltilenler:**

- Kamera/mikrofon izni verilmeden sorular görünüyordu → artık izin verilene kadar gizli.
- Sunucunun 500 ms'lik hız sınırı, ağ gecikmesi oynayınca meşru kareleri 429 ile düşürdü (9 çalıştırmada 1 kare) → 250 ms.
- İzin kaldırma yalnızca iz "ended" ile anlaşılıyordu → site izni değişikliği de dinleniyor.

**Sahte aygıttan kaynaklanan, ürünle ilgisi olmayan takılmalar:** Chrome'un sahte kamerası bazen açıldıktan kısa süre sonra izi kendisi bitiriyor ya da durdurulduktan sonra "bulunamadı" diyor; uygulama bunu doğru şekilde izin kaybı sayıp sınavı izin beklemeye alıyor. Görünür pencere öndeki başka uygulama yüzünden bazen tam ekrana ilk tıklamada geçemedi. `TAB_BLUR` denenemedi çünkü Playwright sekmeleri her zaman "görünür ve odakta" tutuyor.

**Gerçek kamerayla elle denenmesi gerekenler:** 3–4 (başka sekmeye/uygulamaya geçip dönme), 7 (adres çubuğundaki kamera simgesinden izni kaldırma), 9, 11; Firefox ve Safari'nin hepsi.

## Bilinen noktalar

- Tarayıcı, izin kaldırılınca izi her zaman "ended" yapmayabilir; bazı tarayıcılar yalnızca görüntüyü karartıyor. Bu durumda kare yine gidiyor ve analiz (D4) yüzü bulamıyor. 7. adım her tarayıcıda ayrıca denenmeli.
- Tam ekran yalnızca kullanıcı tıklamasıyla açılabiliyor; bu yüzden sınav ekranında düğme var.
