# AI-Guard güvenlik ajanı · Windows kurulum kılavuzu

Sınava girmeden önce bilgisayarınızda AI-Guard güvenlik ajanının çalışıyor olması gerekir. Ajan yalnızca sınav sırasında çalışır; yönetici izni istemez ve bilgisayarınıza bir şey kurmaz.

**Gerekenler:** Windows 10 ya da 11, 64 bit. Başka bir program kurmanız gerekmez.

## Kurulum: 2 adım

### 1. İndirin

Sınav sayfasındaki **"Güvenlik ajanını indir"** bağlantısından `aiguard-agent.exe` dosyasını indirin (yaklaşık 45 MB). Dosyayı İndirilenler klasöründe bırakabilirsiniz.

### 2. Çalıştırın

İndirilen `aiguard-agent.exe` dosyasına çift tıklayın. Siyah bir pencere açılır ve şu yazıyı görürsünüz:

> AI-Guard güvenlik ajanı çalışıyor. Sınav bitene kadar bu pencereyi kapatmayın.

Ajan hazır. Tarayıcıya dönüp sınav sayfasında **"Ajanı eşleştir"** düğmesine basın; eşleştirme kendiliğinden yapılır, kod yazmanız gerekmez.

## "Windows bilgisayarınızı korudu" uyarısı (SmartScreen)

Ajan dosyası dijital olarak imzalanmadığı için Windows ilk çalıştırmada mavi bir uyarı penceresi gösterebilir. Bu, dosyanın az indirildiği anlamına gelir; dosyayı sınav sitesinden indirdiyseniz güvenle devam edebilirsiniz:

1. Uyarı penceresinde **"Ek bilgi"** bağlantısına tıklayın.
2. Görünen **"Yine de çalıştır"** düğmesine basın.

Tarayıcınız (Edge, Chrome) indirme sırasında "Bu dosya yaygın olarak indirilmiyor" derse indirme listesinde dosyanın yanındaki **⋯** menüsünden **"Sakla"** seçeneğini seçin.

## Tarayıcı izni

Eşleştirme sırasında tarayıcı **"Bu cihazdaki diğer uygulamalara ve hizmetlere erişin"** izni isteyebilir. **İzin ver**'i seçin. Yanlışlıkla Engelle'yi seçtiyseniz adres çubuğunun solundaki simgeye tıklayıp izni açın ve sayfayı yenileyin.

## Sınav sayfası bir uyarı gösterirse

Ajan, sınav sırasında açık olmaması gereken bir şey bulursa sınav sayfası nedenini ve ne yapmanız gerektiğini gösterir. Örnekler:

| Uyarı | Ne yapmalı |
|---|---|
| Sınav sırasında açık olmaması gereken bir uygulama çalışıyor (AnyDesk, TeamViewer, Zoom, Teams, Discord, OBS …) | Uygulamayı tamamen kapatın; görev çubuğunun sağındaki simgesi de dahil |
| VPN veya vekil sunucu etkin | VPN bağlantısını kesin ya da Ayarlar → Ağ ve İnternet → Ara sunucu'daki ayarı kapatın |
| Sınav bir sanal makinenin içinde açılmış | Sınava doğrudan kendi bilgisayarınızdan girin |
| Bilgisayarınıza Uzak Masaüstü ile bağlanılmış | Uzak bağlantıyı kapatıp bilgisayarın başından girin |

Sorunu giderdiğinizde kontrol birkaç saniye içinde kendiliğinden yenilenir. Gideremiyorsanız gerekçenizi yazarak devam edebilirsiniz; bu durum oturumunuza işlenir ve sınavınız bir değerlendirici tarafından incelenir.

## Ajan çalışmıyorsa

Ajan hiç açılmıyorsa (ör. kurum bilgisayarında dosya çalıştırma engelliyse) sınav sayfasında gerekçenizi yazarak **ajansız** devam edebilirsiniz. Bu durumda cihaz denetimi yapılmaz ve sınavınız risk skorundan bağımsız olarak incelemeye alınır.

## Sınavdan sonra

Sınav bitince ajan penceresini kapatabilir ve `aiguard-agent.exe` dosyasını silebilirsiniz. Ajan başlangıç programlarına ya da kayıt defterine bir şey eklemez; yalnızca ilk açılışta Windows'un geçici klasörüne (`%TEMP%\.net`) kendi dosyalarını çıkarır.
