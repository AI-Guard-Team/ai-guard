using System.Net;
using Microsoft.Extensions.Configuration;

namespace AIGuard.Infrastructure.Email;

/// <summary>Gönderilecek e-postanın konusu ve iki gövdesi (düz metin + HTML).</summary>
public sealed record EmailMessage(string Subject, string Text, string Html);

/// <summary>
/// Davet ve şifre sıfırlama e-postaları (K-49). Kullanıcının dili bilinmediği için metin önce Türkçe, sonra
/// İngilizce. Bağlantı <c>Web:BaseUrl</c> + önyüz sayfası; ad ve bağlantı HTML'de kaçışlanıyor.
/// </summary>
public static class EmailTemplates
{
    // Vite geliştirme sunucusunun varsayılan adresi; Docker'da Web:BaseUrl ile değiştirilir.
    private const string DefaultWebBaseUrl = "http://localhost:5173";

    public static string Link(IConfiguration configuration, string page, string token)
    {
        var baseUrl = (configuration["Web:BaseUrl"] ?? DefaultWebBaseUrl).TrimEnd('/');
        return $"{baseUrl}/{page}?token={Uri.EscapeDataString(token)}";
    }

    public static EmailMessage Invite(string fullName, string link, DateTimeOffset expiresAt) =>
        Build(
            subject: "AI-Guard hesabınızı etkinleştirin / Activate your AI-Guard account",
            greetingTr: $"Merhaba {fullName},",
            bodyTr: "Kurumunuz sizin için bir AI-Guard hesabı açtı. Aşağıdaki bağlantıdan şifrenizi belirleyerek hesabınızı etkinleştirin.",
            buttonTr: "Hesabımı etkinleştir",
            noteTr: $"Bağlantı tek kullanımlıktır ve {Stamp(expiresAt)} (UTC) tarihine kadar geçerlidir. Bu daveti beklemiyorsanız e-postayı yok sayın.",
            greetingEn: $"Hello {fullName},",
            bodyEn: "Your institution has opened an AI-Guard account for you. Set your password with the link below to activate it.",
            buttonEn: "Activate my account",
            noteEn: $"The link can be used once and is valid until {Stamp(expiresAt)} (UTC). If you were not expecting this invitation, ignore this email.",
            link: link);

    public static EmailMessage PasswordReset(string fullName, string link, DateTimeOffset expiresAt) =>
        Build(
            subject: "AI-Guard şifre sıfırlama / AI-Guard password reset",
            greetingTr: $"Merhaba {fullName},",
            bodyTr: "Şifrenizi sıfırlamak için bir istek aldık. Yeni şifrenizi aşağıdaki bağlantıdan belirleyin.",
            buttonTr: "Şifremi sıfırla",
            noteTr: $"Bağlantı tek kullanımlıktır ve {Stamp(expiresAt)} (UTC) tarihine kadar geçerlidir. Bu isteği siz yapmadıysanız e-postayı yok sayın; şifreniz değişmez.",
            greetingEn: $"Hello {fullName},",
            bodyEn: "We received a request to reset your password. Set a new password with the link below.",
            buttonEn: "Reset my password",
            noteEn: $"The link can be used once and is valid until {Stamp(expiresAt)} (UTC). If you did not request this, ignore this email; your password will not change.",
            link: link);

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static EmailMessage Build(
        string subject,
        string greetingTr, string bodyTr, string buttonTr, string noteTr,
        string greetingEn, string bodyEn, string buttonEn, string noteEn,
        string link)
    {
        var text = string.Join("\n", greetingTr, "", bodyTr, link, "", noteTr, "", "———", "", greetingEn, "", bodyEn, link, "", noteEn, "", "AI-Guard");

        static string E(string value) => WebUtility.HtmlEncode(value);
        string Section(string greeting, string body, string button, string note) => $"""
            <p style="margin:0 0 12px;font-size:16px;color:#111a38">{E(greeting)}</p>
            <p style="margin:0 0 20px;font-size:15px;line-height:1.55;color:#4e5b78">{E(body)}</p>
            <p style="margin:0 0 20px"><a href="{E(link)}" style="display:inline-block;padding:12px 22px;border-radius:999px;background:#4361ee;color:#ffffff;font-weight:600;text-decoration:none">{E(button)}</a></p>
            <p style="margin:0;font-size:13px;line-height:1.5;color:#66728d">{E(note)}</p>
            """;

        var html = $"""
            <!doctype html>
            <html lang="tr"><body style="margin:0;padding:24px;background:#f4f7fc;font-family:Segoe UI,Arial,sans-serif">
            <div style="max-width:560px;margin:0 auto;padding:32px;border-radius:18px;background:#ffffff">
            <p style="margin:0 0 24px;font-size:18px;font-weight:800;color:#14214d">AI-Guard</p>
            {Section(greetingTr, bodyTr, buttonTr, noteTr)}
            <hr style="margin:28px 0;border:0;border-top:1px solid #e3e9f4">
            <div lang="en">{Section(greetingEn, bodyEn, buttonEn, noteEn)}</div>
            </div></body></html>
            """;

        return new EmailMessage(subject, text, html);
    }
}
