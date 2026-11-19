# Y1 · Tarayıcı deneme notları (yerel ağ erişimi)

Platform sitesindeki sayfa, kodu `http://127.0.0.1:47321/pair` adresindeki ajana gönderiyor. Tarayıcılar, bir web sitesinin kullanıcının bilgisayarındaki (loopback) veya yerel ağdaki bir adrese istek atmasını giderek daha sıkı denetliyor. Bu belge Chrome, Edge ve Firefox'ta bu isteğin nasıl davrandığını not etmek için.

*Son güncelleme: 20 Kasım 2026 (Y1 revizyonu)*

## Ajan tarafında yapılanlar

| Kural | Neden |
|---|---|
| Yalnızca `127.0.0.1:47321` dinleniyor | Ağdaki başka bir bilgisayar ajana bağlanamaz; kod tarayıcıdan ajana yalnızca bu yoldan gelir |
| Origin `Agent:AllowedOrigins` listesinde değilse 403 | CORS başlığı yalnızca yanıtın okunmasını engeller, isteği değil; izinsiz site kod gönderemesin |
| Origin başlığı yoksa 403 | Eşleştirme yalnızca tarayıcıdan |
| `Host` başlığı `127.0.0.1` veya `localhost` değilse 400 | DNS rebinding: kötü niyetli bir alan adı 127.0.0.1'e çözülse bile reddedilir |
| Ön kontrol (OPTIONS) yanıtında `Access-Control-Allow-Private-Network: true` | Chrome'un Private Network Access kuralı (kart) |
| Yalnızca `application/json` gövde | `text/plain` gibi "basit" istekler ön kontrolsüz gider; JSON zorunlu olunca her istekte ön kontrol yapılır |

## Komut satırıyla doğrulananlar (20 Kasım 2026, Windows 11)

API Docker'da (Development), ajan Windows'ta (`aiguard-agent.exe`) çalışırken `curl` ile:

| Deneme | Sonuç |
|---|---|
| `netstat`: dinlenen adres | yalnızca `127.0.0.1:47321` |
| Bilgisayarın ağ IP'si (`192.168.56.1:47321`) üzerinden bağlanma | bağlantı kurulamadı |
| İzinsiz origin'den `POST /pair` | 403, kod sunucuya gitmedi |
| İzinli origin'den ön kontrol | 204, `Access-Control-Allow-Origin` ve `Access-Control-Allow-Private-Network: true` var |
| İzinli origin'den `POST /pair` | 200 `{"status":"paired"}`, veritabanında `paired_at` dolu, yalnızca kodun özeti saklı |
| Aynı kodla ikinci `POST /pair` | 409 `{"status":"rejected"}` |

## Tarayıcı denemesi nasıl yapılır

1. API'yi Development ortamında çalıştır (README: yerel kurulum veya Docker).
2. Ajanı çalıştır: `cd agent && dotnet run --project src/AIGuard.Agent.Windows` (macOS'ta `AIGuard.Agent.MacOS`). `appsettings.json` içindeki `AllowedOrigins` varsayılanı `http://localhost:5173`.
3. Test sayfasını sun: `python -m http.server 5173 --bind 127.0.0.1 -d agent/tools` ve tarayıcıda `http://localhost:5173/pairing-test.html` adresini aç.
4. Bir oturum için kod al (E2 gelene kadar uç yalnızca Development'ta açık):
   ```bash
   curl -X POST http://localhost:5080/api/sessions/<oturum-id>/agent-code
   ```
5. Yanıttaki `code` değerini sayfaya yapıştır, "Ajana gönder"e bas. Kod 5 dakika geçerli ve tek kullanımlık. Oturum `AgentPairing` durumunda olmalı.
6. DevTools > Network'te `OPTIONS` ve `POST` isteklerini, Console'da varsa uyarıları not et.

**İzin istemini görmek için sayfa genel (public) bir adresten açılmalı.** `localhost` → `127.0.0.1` isteği tarayıcı için aynı adres alanında kalıyor; istem çıkmaz. Genel adresten denemek için test sayfası geçici bir HTTPS adresine konabilir (ör. GitHub Pages veya bir tünel). O adres ajanın `AllowedOrigins` listesine eklenmeli.

## Beklenen davranış (denemeyle doğrulanmalı)

Aşağıdakiler tarayıcıların yayımladığı belgelere göre beklenen davranış; tablodaki "Sonuç" sütunu denemeden sonra doldurulacak.

- **Chrome:** Private Network Access'in (ön kontrol + `Access-Control-Allow-Private-Network`) yerini "Local Network Access" izin istemi aldı. Genel bir siteden loopback'e ilk istekte kullanıcıya "yerel ağdaki cihazlara erişim" izni soruluyor. Reddedilirse `fetch` hata veriyor. Ajan `Access-Control-Allow-Private-Network` başlığını yine de gönderiyor; eski sürümlerle uyum için zararsız.
- **Edge:** Chromium tabanlı olduğu için Chrome ile aynı davranması bekleniyor.
- **Firefox:** Yerel ağ erişimi kısıtlaması daha sınırlı; istemsiz geçmesi bekleniyor. CORS kuralları aynı şekilde uygulanıyor.
- **Karma içerik:** `https://` sayfadan `http://127.0.0.1` adresine istek üç tarayıcıda da izinli, çünkü `127.0.0.1` "güvenilir kaynak" sayılıyor. (Safari bunu engelliyor; Safari kartta yok.)

| Tarayıcı / sürüm | Sayfa adresi | İzin istemi çıktı mı? | İzin verilince | Reddedilince | Not |
|---|---|---|---|---|---|
| Chrome … | `http://localhost:5173` | | | | |
| Chrome … | genel HTTPS adres | | | | |
| Edge … | `http://localhost:5173` | | | | |
| Edge … | genel HTTPS adres | | | | |
| Firefox … | `http://localhost:5173` | | | | |
| Firefox … | genel HTTPS adres | | | | |

## Web önyüzü için notlar (Şeyma, S2)

Kart Y1, Chrome izin isteminin sonucunun aday ekranındaki yönlendirme metni için S2'ye iletilmesini istiyor. Tablo doldurulunca metin ona göre netleşecek; şimdilik öneri:

> Sınav güvenlik uygulamasıyla bağlantı kuruluyor. Tarayıcınız "yerel ağdaki cihazlara erişim" izni isterse **İzin ver**'i seçin. Bağlantı kurulamazsa güvenlik uygulamasının açık olduğundan emin olup sayfayı yenileyin.

Diğer notlar:

- Kod adaya gösterilmez, konsola veya hata mesajına yazılmaz.
- Ajan yanıtları: `200 paired`, `409 rejected` (yeni kod alıp tekrar dene), `502 unavailable` (sunucuya ulaşılamadı), `403` (sayfanın origin'i ajanın listesinde yok). Ajan kapalıysa `fetch` hata fırlatır (Y4: ajansız devam akışı).
- İzin istemi reddedildiğinde `fetch` ile "ajan kapalı" durumu tarayıcıda ayırt edilemiyor; adaya ikisi için de "izni kontrol et / ajanı başlat" yönlendirmesi gösterilmeli.
