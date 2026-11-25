# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Üç rol, üçü eşit önemde:

- **Aday:** Çevrim içi sınava giren öğrenci ya da çalışan. Sınav öncesinde ve sırasında stresli; rıza, ajan kurulumu ve kimlik doğrulama gibi alışık olmadığı adımlardan geçiyor. İşi: adımları anlayıp sınavı sorunsuz tamamlamak.
- **Değerlendirici:** Kurumun atadığı kişi. Riskli ya da işaretli oturumların dosyalarını inceliyor, yanlış alarmları ayıklıyor ve karar veriyor (Temiz / Şüpheli / İhlal).
- **Yönetici:** Kurumun platform sorumlusu. Sınav, soru, atama ve kullanıcı kuruyor; rapor, ayar ve denetim kaydına bakıyor.

## Product Purpose

AI-Guard, çevrim içi sınavlar için sıfır güven (zero-trust) kopya tespit platformu (Doğuş Üniversitesi YM 491 Araştırma Projesi). Kamera, ses, tarayıcı, cihaz ajanı ve ağdan gelen sinyalleri açıklanabilir bir risk skoruna çeviriyor; kopya kararını sistem değil, insan değerlendirici veriyor. Başarı: aday adil ve rahat bir sınav geçirir, değerlendirici neden şüphelenildiğini hızlıca görür, kurum KVKK'ya uygun ve denetlenebilir bir süreç yürütür.

## Positioning

Kararı makine vermez: her olay gerekçesi, kanıtı ve skora katkısıyla gösterilir, değerlendirici yanlış alarmı skordan çıkarır. Sürekli kayıt yerine yalnızca şüpheli anların kanıtı, saklama süresi dolunca silinir; biyometrik işlem açık rızayla.

## Operating Context

- Aday masaüstü tarayıcıda (Windows/macOS) kamera ve mikrofon açık, cihazında güvenlik ajanı (`http://127.0.0.1:47321`) ile sınava girer; sınav öncesi sihirbaz: rıza → ajan eşleştirme → hazırlık → kimlik doğrulama.
- Değerlendirici ve yönetici masaüstünde, uzun oturumlarla çalışır.
- Arayüz Türkçe; İngilizce desteği planlı.

## Capabilities and Constraints

- Ortak adlar (oturum durumları, olay türleri, roller, uçlar) `CLAUDE.md` ve `docs/MIMARI-KARARLAR.md`'de; değiştirilmez.
- Ekranda sunucuda karşılığı olmayan bilgi, sahte istatistik ya da uydurma iddia gösterilmez (K-34, K-38).
- Kartta olmayan özellik eklenmez; gerekirse önce sorulur.
- CSP: yalnızca kendi kaynakları; yazı tipleri pakette, CDN yok.

## Brand Commitments

- Ad: **AI-Guard** (kesin). Logo ve ikon yenilenebilir.
- Renk: lacivert ana renk; kurumsal İK havası, göze hitap eden ve güven veren tonlar.
- Referans (kullanıcının verdiği): hiringcycle.ai — modern, sıcak, kurumsal İK platformu hissi; işçilik çıtası bu.
- Yön bulma: sayfaları metinle boğmadan, "i" bilgi düğmeleri, ilk girişte rol bazlı tanıtım (onboarding), ayrıntı için açılır pencere ve ayrı ekranlar. Her işi tek ekrana sığdırmak zorunlu değil.
- Animasyon kullanılabilir; Lottie animasyonları sonra eklenecek (boş durumlar ve tanıtım için yer bırakılır).

## Evidence on Hand

- Gerçek müşteri, istatistik, referans ya da başarı oranı yok; ekranda uydurulmaz.
- Yerel deneme verisi: `DevDataSeeder` (örnek sınav, 4 kullanıcı).

## Product Principles

1. Kullanıcı her ekranda nerede olduğunu, ne yapacağını ve sıradaki adımı bilir.
2. Kararı insan verir; sistem gerekçesini ve kanıtını açıkça gösterir.
3. Aday için sakin ve yönlendirici; kurum kullanıcıları için hızlı ve net.
4. Gizlilik görünür: rıza, saklama süresi ve denetim kaydı saklanmaz, anlatılır.

## Accessibility & Inclusion

WCAG 2.2 AA: renk tek başına anlam taşımaz, klavyeyle kullanılabilir, hareket azaltma tercihine uyulur. Türkçe karakterleri tam destekleyen yazı tipi.
