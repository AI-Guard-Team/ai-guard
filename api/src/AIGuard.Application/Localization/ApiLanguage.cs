namespace AIGuard.Application.Localization;

/// <summary>
/// İsteğin arayüz dili (K-48). Api katmanı <c>Accept-Language</c> başlığına göre istek başında ayarlıyor; değer
/// istek boyunca (async akışta) geçerli. Varsayılan Türkçe: başlık yoksa, tanınmıyorsa ya da istek dışında
/// (arka plan işi, test) metinler Türkçe. İşletim sisteminin dili ve sayı/tarih kültürü etkilenmiyor.
/// </summary>
public static class ApiLanguage
{
    private static readonly AsyncLocal<bool> English = new();

    public static bool IsEnglish => English.Value;

    public static void Set(bool english) => English.Value = english;
}
