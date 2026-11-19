namespace AIGuard.Application.Abstractions;

/// <summary>
/// Şifre özetleme. Giriş, şifre belirleme ve örnek veri aynı uygulamayı kullanıyor;
/// yöntem değişirse yalnızca Infrastructure'daki sınıf değişiyor.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Şifre özetle eşleşmiyorsa veya özet bozuksa false döner.</summary>
    bool Verify(string passwordHash, string password);
}
