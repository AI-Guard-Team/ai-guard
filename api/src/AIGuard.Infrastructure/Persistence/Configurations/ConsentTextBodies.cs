namespace AIGuard.Infrastructure.Persistence.Configurations;

/// <summary>
/// İlk yayımlanan aydınlatma ve rıza metinleri (A5). İçerik GAB 3.3.8'deki veri yaşam döngüsü tablosuna göre
/// yazıldı. Bu metinler yayımlandıktan sonra değiştirilmez: metin değişecekse yeni sürüm göçle eklenir (K-32).
/// </summary>
/// <remarks>
/// Gerçek kullanımda metnin kurumun hukuk birimince gözden geçirilmesi gerekiyor (GAB 3.3.8).
/// </remarks>
internal static class ConsentTextBodies
{
    public const string BiometricV1 =
        """
        Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu (KVKK) kapsamında kayıt fotoğrafınızdan elde edilen biyometrik verinin nasıl işlendiğini açıklar.

        İşlenen veri: Yüklediğiniz kayıt fotoğrafından bir yüz vektörü (sayısal yüz şablonu) çıkarılır. Fotoğrafın kendisi saklanmaz; vektör çıkarıldıktan hemen sonra silinir. Yüz vektörü KVKK'ya göre özel nitelikli kişisel veridir.

        Amaç: Yüz vektörü yalnızca sınav başında ve sınav sırasında kimliğinizi doğrulamak, yani sınava sizin girdiğinizi kontrol etmek için kullanılır.

        Hukuki sebep: Açık rızanız (KVKK madde 6).

        Saklama: Yüz vektörü şifreli olarak saklanır. Son sınavınızdan 30 gün sonra ya da rızanızı geri aldığınızda silinir.

        Erişim: Yüz vektörünü hiçbir kullanıcı göremez ve indiremez; yalnızca sistem kimlik eşleştirmesi için kullanır.

        Rızanızı geri alma: Rızanızı profil sayfanızdan istediğiniz zaman geri alabilirsiniz. Bu durumda yüz vektörünüz silinir ve süren sınav oturumunuz iptal edilir. Rıza vermemeniz ya da geri almanız durumunda sınavı biyometrik olmayan alternatif bir yöntemle yapmak için sınavı düzenleyen birime başvurabilirsiniz.

        Haklarınız: KVKK madde 11 kapsamındaki haklarınızı (bilgi talep etme, düzeltme, silme vb.) kullanmak için sınavı düzenleyen birime başvurabilirsiniz.

        Yukarıdaki bilgileri okudum. Kayıt fotoğrafımdan elde edilecek yüz vektörünün kimlik doğrulama amacıyla işlenmesine açık rıza veriyorum.
        """;

    public const string SessionV1 =
        """
        Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu (KVKK) kapsamında sınav sırasında hangi verilerin işlendiğini açıklar.

        İşlenen veriler:
        - Kamera görüntüsü ve mikrofon sesi sınav sırasında anlık olarak analiz edilir (yüz, bakış yönü, telefon, konuşma). Sürekli video veya ses kaydı tutulmaz; analiz edilen kareler ve ses parçaları analizden hemen sonra silinir.
        - Kural dışı bir durum tespit edildiğinde (olay) o ana ait kamera karesi kanıt olarak şifreli saklanır.
        - Tarayıcı olayları (sekme değiştirme, tam ekrandan çıkma, kopyala-yapıştır), güvenlik ajanının bulduğu göstergeler (sanal makine, yasaklı uygulama, VPN) ve bağlantı bilgileri kaydedilir.
        - Sınav başında ve sınav sırasında yüz doğrulaması yapılır.

        Amaç: Sınavın kurallara uygun yapıldığını doğrulamak. Sistem kopya kararı vermez; tespit edilen olaylar ve risk skoru gözetmenin değerlendirmesine sunulur, nihai karar gözetmene aittir.

        Hukuki sebep: Açık rızanız (KVKK madde 6).

        Saklama: Kanıt kareleri, olaylar ve cihaz göstergeleri sınavın saklama süresi (varsayılan 30 gün) sonunda otomatik olarak silinir. Sınav sonucunuz ve gözetmen kararı saklanmaya devam eder. Rıza ve denetim kayıtları ispat amacıyla 1 yıl saklanır.

        Erişim: Kanıt karelerine ve olaylara yalnızca sınava atanmış gözetmen ve yönetici erişebilir.

        Rıza vermezseniz: Sınav başlamaz ve sınav hakkınız kullanılmış sayılmaz. Sınavı alternatif bir yöntemle yapmak için sınavı düzenleyen birime başvurabilirsiniz.

        Haklarınız: KVKK madde 11 kapsamındaki haklarınızı kullanmak için sınavı düzenleyen birime başvurabilirsiniz.

        Yukarıdaki bilgileri okudum. Bu sınav oturumu süresince kamera, mikrofon ve cihaz verilerimin yukarıdaki amaçla işlenmesine açık rıza veriyorum.
        """;
}
