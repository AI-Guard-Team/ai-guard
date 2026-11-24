using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace AIGuard.Api.Auth;

/// <summary>
/// GAB 3.3.8 erişim matrisinin uç bazında uygulanışı (A5, K-32). Her policy matrisin bir satırı; uçlar rol adı
/// yerine bu adları kullanıyor, böylece "kim neye erişir" tek yerde ve testle korunuyor (AccessMatrixTests).
/// </summary>
/// <remarks>
/// Policy yalnızca rolü kontrol ediyor. Kayıt düzeyindeki kural (aday yalnızca kendi oturumu, değerlendirici
/// yalnızca atandığı sınav) servislerde: <c>SessionAccessService</c>, <c>ReviewRules</c> (K-20, K-23, K-31).
/// Hiçbir policy referans yüz vektörüne erişim vermiyor; matriste bu satır herkes için "—" ve öyle bir uç yok.
/// </remarks>
public static class AccessPolicies
{
    /// <summary>Atandığı sınavı çözme, kendi rızasını verme ve geri alma: Aday.</summary>
    public const string TakeExam = nameof(TakeExam);

    /// <summary>Kanıt karelerini ve olayları inceleme, rapor: atandığı sınavda Gözetmen, Yönetici.</summary>
    public const string ReviewRead = nameof(ReviewRead);

    /// <summary>Yanlış alarm işaretleme, not ve karar kaydetme: yalnızca atandığı sınavda Gözetmen.</summary>
    public const string ReviewDecide = nameof(ReviewDecide);

    /// <summary>Kullanıcı, sınav, soru, atama ve saklama süresi yönetimi: Yönetici.</summary>
    public const string Manage = nameof(Manage);

    /// <summary>Denetim kaydını görüntüleme: Yönetici, yalnızca okuma.</summary>
    public const string ReadAuditLog = nameof(ReadAuditLog);

    public static void Add(AuthorizationOptions options)
    {
        options.AddPolicy(TakeExam, p => p.RequireRole(nameof(UserRole.Candidate)));
        options.AddPolicy(ReviewRead, p => p.RequireRole(nameof(UserRole.Reviewer), nameof(UserRole.Admin)));
        options.AddPolicy(ReviewDecide, p => p.RequireRole(nameof(UserRole.Reviewer)));
        options.AddPolicy(Manage, p => p.RequireRole(nameof(UserRole.Admin)));
        options.AddPolicy(ReadAuditLog, p => p.RequireRole(nameof(UserRole.Admin)));
    }
}
