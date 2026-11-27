# AI-Guard güvenlik ajanı · macOS kurulum kılavuzu

Sınava girmeden önce bilgisayarınızda AI-Guard güvenlik ajanının çalışıyor olması gerekir. Ajan yalnızca sınav sırasında çalışır; arka planda kendiliğinden başlamaz ve oturum açılışına bir şey eklemez.

**Gerekenler:** macOS 12 (Monterey) veya daha yeni bir sürüm. Mac'iniz Apple Silicon (M1, M2, M3 …) ya da Intel olabilir; Apple menüsü → **Bu Mac Hakkında** ekranında yazar. Başka bir program kurmanız gerekmez.

## Kurulum: 3 adım

### 1. İndirin

Sınav sayfasındaki **"Güvenlik ajanını indir"** bağlantısından Mac'inize uygun kurulum paketini indirin (her biri yaklaşık 40 MB):

| Mac'iniz | Paket |
|---|---|
| Apple Silicon (M1, M2, M3 …) | `aiguard-agent-arm64.pkg` |
| Intel | `aiguard-agent-x64.pkg` |

### 2. Kurun

İndirilen `.pkg` dosyasına çift tıklayın ve ekrandaki adımları izleyin. Kurulum ajanı `/Applications/AI-Guard Ajanı` klasörüne kopyalar; bu klasöre yazmak için Mac'inizin parolası istenir.

Kurulum bitince Installer "kurulum paketini Çöp Sepeti'ne taşımak ister misiniz?" diye sorar. **Taşı** ya da **Sakla**, ikisi de olur; paket ajanın çalışması için gerekmez.

> **"Açılamıyor, çünkü Apple tarafından doğrulanamadı" uyarısı çıkarsa:** aşağıdaki [Gatekeeper uyarısı](#gatekeeper-uyarısı) bölümüne bakın.

### 3. Çalıştırın

Finder'da **Programlar → AI-Guard Ajanı** klasörünü açın ve **`Ajanı Başlat.command`** dosyasına çift tıklayın. Terminal penceresi açılır ve şu yazıyı görürsünüz:

> AI-Guard güvenlik ajanı çalışıyor. Sınav bitene kadar bu pencereyi kapatmayın.

Ajan hazır. Tarayıcıya dönüp sınav sayfasında **"Ajanı eşleştir"** düğmesine basın; eşleştirme kendiliğinden yapılır, kod yazmanız gerekmez.

İlk çalıştırmada ajan dosyalarını kendi geçici klasörüne çıkardığı için açılış birkaç saniye sürebilir.

## Gatekeeper uyarısı

Kurulum paketi ve ajan Apple Developer ID ile imzalanmadığı için macOS (Gatekeeper) ilk açılışta uyarı gösterebilir. Dosyayı sınav sitesinden indirdiyseniz güvenle devam edebilirsiniz.

### Paketi açarken ("… doğrulanamadı")

1. Uyarı penceresinde **Bitti**'ye (ya da **Tamam**'a) basın; paketi Çöp Sepeti'ne taşımayın.
2. **Sistem Ayarları → Gizlilik ve Güvenlik** bölümünü açın.
3. Sayfayı aşağı kaydırın; "`aiguard-agent-….pkg` engellendi" satırının yanındaki **Yine de Aç** düğmesine basın.
4. Mac parolanızı girin ve kurulumu sürdürün.

Eski macOS sürümlerinde (Monterey, Ventura) paketin üzerine sağ tıklayıp (ya da `control` basılı tutarak tıklayıp) **Aç**'ı seçmek de yeterlidir.

### `Ajanı Başlat.command` dosyasını açarken

Paketle kurulan dosyada indirme işareti (karantina) bulunmadığından genellikle uyarı çıkmaz. Yine de uyarı çıkarsa yukarıdaki **Yine de Aç** adımlarını aynen uygulayın.

Her şey başarısız olursa Terminal'de şunu çalıştırabilirsiniz:

```bash
"/Applications/AI-Guard Ajanı/aiguard-agent"
```

## Tarayıcı izni

Eşleştirme sırasında tarayıcı **"Yerel ağdaki cihazlara erişim"** (Chrome / Edge: "Bu cihazdaki diğer uygulamalara ve hizmetlere erişin") izni isteyebilir. **İzin ver**'i seçin. Yanlışlıkla Engelle'yi seçtiyseniz adres çubuğunun solundaki simgeye tıklayıp izni açın ve sayfayı yenileyin. Safari kullanıyorsanız Safari → Ayarlar → Web siteleri bölümünden de izne bakabilirsiniz.

## Sınav sayfası bir uyarı gösterirse

Ajan, sınav sırasında açık olmaması gereken bir şey bulursa sınav sayfası nedenini ve ne yapmanız gerektiğini gösterir. Örnekler:

| Uyarı | Ne yapmalı |
|---|---|
| Sınav sırasında açık olmaması gereken bir uygulama çalışıyor (AnyDesk, TeamViewer, Zoom, Teams, Discord, OBS …) | Uygulamayı tamamen kapatın (⌘Q); menü çubuğundaki simgesi de dahil |
| macOS Ekran Paylaşımı açık | Sistem Ayarları → Genel → Paylaşım bölümünde **Ekran Paylaşımı**'nı kapatın |
| VPN bağdaştırıcısı etkin | VPN uygulamanızdan bağlantıyı kesin |
| Vekil sunucu (macOS ayarı) ya da otomatik yapılandırma betiği tanımlı | Sistem Ayarları → Ağ → kullandığınız bağlantı → **Ayrıntılar → Vekil Sunucular** bölümünden vekil ayarlarını kapatın |
| Sınav bir sanal makinenin içinde açılmış | Sınava doğrudan kendi Mac'inizden girin |

Sorunu giderdiğinizde kontrol birkaç saniye içinde kendiliğinden yenilenir. Gideremiyorsanız gerekçenizi yazarak devam edebilirsiniz; bu durum oturumunuza işlenir ve sınavınız bir değerlendirici tarafından incelenir.

> macOS'un kendi kullandığı boştaki ağ arayüzleri (`utun0`, `utun1` …) VPN sayılmaz. Uyarı yalnızca internete giden trafik bir tünel üzerinden geçiyorsa verilir.

## Ajan çalışmıyorsa

Ajan hiç açılmıyorsa (ör. kurum Mac'inde program kurmak engelliyse) sınav sayfasında gerekçenizi yazarak **ajansız** devam edebilirsiniz. Bu durumda cihaz denetimi yapılmaz ve sınavınız risk skorundan bağımsız olarak incelemeye alınır.

## Sınavdan sonra

Sınav bitince Terminal penceresini kapatabilirsiniz (ya da pencerede `control` + `C`). İsterseniz ajanı tamamen kaldırabilirsiniz:

```bash
sudo rm -rf "/Applications/AI-Guard Ajanı"
rm -rf ~/.net/aiguard-agent
sudo pkgutil --forget tr.edu.aiguard.agent
```

Ajan oturum açılışına, Başlatma Öğelerine ya da başka bir yere bir şey eklemez; yalnızca ilk açılışta kendi dosyalarını kullanıcı klasörünüzdeki `~/.net/aiguard-agent` klasörüne çıkarır.

---

## Yöneticiler için: paketi üretme

Paket, sınavın çalıştığı adrese göre üretilir (adres pakete gömülür; öğrenci bir şey ayarlamaz). Apple Silicon ve Intel paketleri ayrı ayrı üretilir. Paket yalnızca macOS'ta üretilebilir (`pkgbuild` gerekir) ve .NET 8 SDK kurulu olmalıdır.

```bash
agent/scripts/publish-macos.sh \
  --api-base-url https://sinav.okul.edu.tr \
  --allowed-origin https://sinav.okul.edu.tr
```

Varsayılan olarak `agent/dist/macos` altına iki paket de yazılır. Yalnızca birini üretmek için `--arch arm64` ya da `--arch x64` verin. `--api-base-url` `https` olmak zorundadır (yalnızca `localhost` için `http` kabul edilir). Ayrıntılar için `agent/README.md`'e bakın.

Paket Developer ID ile imzalanmamıştır ve noter onayından (notarization) geçmemiştir; bu yüzden yukarıdaki Gatekeeper adımları gerekir. Apple Developer hesabı olursa `productsign` ve `notarytool` ile imzalanıp bu uyarı kaldırılabilir.
