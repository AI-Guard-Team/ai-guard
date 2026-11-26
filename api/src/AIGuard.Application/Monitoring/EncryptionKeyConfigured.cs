using AIGuard.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AIGuard.Application.Monitoring;

/// <summary>
/// Şifreleme anahtarı tanımlı mı (K-26). Kamera izleme kimlik vektörünü çözmeden önce soruyor: anahtarsız
/// bilgisayarda o oturumda kimlik kontrolü kapanıyor, kare analizi sürüyor (K-45).
/// </summary>
/// <remarks>
/// Yalnızca anahtar eksikliğini ayırt eder. Depolamanın çözümlenmesi ve vektörün yüklenmesi yakalanmıyor; oradaki
/// başka hatalar (DI kaydı, veritabanı) istisna olarak yükseliyor ve gizlenmiyor.
/// </remarks>
public delegate bool EncryptionKeyConfigured();

public static class EncryptionKey
{
    /// <summary>
    /// Kayıtlı <see cref="IFieldEncryptor"/> factory'si (AesGcmFieldEncryptor.FromConfiguration) yalnızca anahtar
    /// tanımlı değilse ya da 32 baytlık Base64 değilse <see cref="InvalidOperationException"/> fırlatıyor; burada
    /// yakalanan tek durum bu. Şifreleyici hiç kayıtlı değilse <c>true</c>: kayıt hatası depolama çözümlenirken
    /// görünür olsun, anahtar eksikliği sanılmasın.
    /// </summary>
    public static bool IsConfigured(IServiceProvider services)
    {
        try
        {
            services.GetService<IFieldEncryptor>();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
