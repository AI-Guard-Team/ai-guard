using System.Text.RegularExpressions;
using AIGuard.Application.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Localization;

/// <summary>
/// Sunucunun kullanıcıya dönen metinlerinin İngilizcesi (K-48). Metinler kodda Türkçe kalıyor (asıl dil); istek
/// İngilizceyse ProblemDetails'in <c>title</c> ve <c>detail</c>'ı burada çevriliyor. Değişkenli cümleler kalıpla.
/// <c>ServerTextsCoverageTests</c> kaynak koddaki her Türkçe hata metninin burada karşılığı olduğunu denetliyor:
/// yeni bir metin eklenince İngilizcesi de buraya eklenmeli.
/// </summary>
public static partial class ServerTexts
{
    private static readonly Dictionary<string, string> Exact = new(StringComparer.Ordinal)
    {
        // ---- Kimlik ----
        ["E-posta veya şifre hatalı."] = "Incorrect email or password.",
        ["Giriş başarısız."] = "Sign-in failed.",
        ["Hesap geçici olarak kilitlendi."] = "Account temporarily locked.",
        ["Oturum süresi doldu."] = "Session expired.",
        ["Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın."] = "Your session has expired. Please sign in again.",
        ["Geçersiz davet."] = "Invalid invitation.",
        ["Davet bağlantısı geçersiz, kullanılmış veya süresi dolmuş. Yöneticinizden yeni davet isteyin."] =
            "The invitation link is invalid, already used or expired. Ask your administrator for a new invitation.",
        ["Geçersiz bağlantı."] = "Invalid link.",
        ["Sıfırlama bağlantısı geçersiz, kullanılmış veya süresi dolmuş. Yeni bir bağlantı isteyin."] =
            "The reset link is invalid, already used or expired. Request a new link.",
        ["Mevcut şifre hatalı."] = "Current password is incorrect.",
        ["Mevcut şifreniz hatalı."] = "Your current password is incorrect.",
        ["Şifre kurala uymuyor."] = "The password does not meet the rules.",
        ["Şifre yalnızca boşluktan oluşamaz."] = "The password cannot be only spaces.",
        ["Belirteçte kullanıcı bilgisi eksik."] = "The token is missing user information.",
        ["Kullanıcı pasif veya rolü değişmiş."] = "The user is inactive or their role has changed.",

        // ---- Genel ----
        ["Geçersiz istek."] = "Invalid request.",
        ["Geçersiz sorgu."] = "Invalid query.",
        ["questionId ve pozitif bir seq zorunludur."] = "questionId and a positive seq are required.",
        ["Çakışma."] = "Conflict.",
        ["İşlem yapılamıyor."] = "The action cannot be performed.",
        ["Devam edilemiyor."] = "Cannot continue.",
        ["Destek talep edin."] = "Request support.",
        ["Yeniden deneyin; sorun sürerse destek talep edin."] = "Try again; if the problem continues, request support.",

        // ---- Kullanıcılar ----
        ["E-posta kullanımda."] = "Email already in use.",
        ["Bu e-posta adresiyle bir hesap zaten var."] = "An account with this email address already exists.",
        ["Ad soyad 1–200 karakter olmalıdır."] = "Full name must be 1–200 characters.",
        ["Geçerli bir e-posta adresi girin."] = "Enter a valid email address.",
        ["Rol Admin, Reviewer veya Candidate olmalıdır."] = "Role must be Admin, Reviewer or Candidate.",
        ["Değiştirilecek alan yok."] = "There is nothing to change.",
        ["Kendi rolünüzü veya durumunuzu değiştiremezsiniz."] = "You cannot change your own role or status.",
        ["Kullanıcı daveti kabul etmiş; davet yeniden gönderilemez."] = "The user has already accepted the invitation; it cannot be sent again.",
        ["Pasif kullanıcıya davet gönderilemez."] = "An invitation cannot be sent to an inactive user.",
        ["Davet az önce gönderildi; birkaç dakika sonra yeniden deneyin."] = "The invitation was just sent; try again in a few minutes.",

        // ---- Sınavlar, sorular, atamalar ----
        ["Bu sınavda oturum başlamış; sınav ve sorular artık değiştirilemez."] = "Sessions have started in this exam; the exam and its questions can no longer be changed.",
        ["Bu sınavda oturum başlamış; sınav silinemez."] = "Sessions have started in this exam; it cannot be deleted.",
        ["Bu sınavda oturum başlamış; risk ayarları artık değiştirilemez."] = "Sessions have started in this exam; its risk settings can no longer be changed.",
        ["Sınav adı 1–200 karakter olmalıdır."] = "Exam name must be 1–200 characters.",
        ["Sınav türü GeneralAptitude veya EnglishProficiency olmalıdır."] = "Exam type must be GeneralAptitude or EnglishProficiency.",
        ["Sınav süresi 1–600 dakika olmalıdır."] = "Exam duration must be 1–600 minutes.",
        ["Erişim penceresinin başlangıç ve bitiş zamanı zorunludur."] = "The access window start and end times are required.",
        ["Erişim penceresinin bitişi başlangıcından sonra olmalıdır."] = "The access window must end after it starts.",
        ["Erişim penceresi sınav süresinden kısa olamaz."] = "The access window cannot be shorter than the exam duration.",
        ["Saklama süresi 1–3650 gün olmalıdır."] = "Retention must be 1–3650 days.",
        ["Doğru seçenek, seçeneklerden biri olmalıdır."] = "The correct option must be one of the options.",
        ["Sıra negatif olamaz."] = "Order cannot be negative.",
        ["Kullanıcı ve rol (Candidate veya Reviewer) zorunludur."] = "User and role (Candidate or Reviewer) are required.",
        ["Kullanıcı bulunamadı veya pasif."] = "User not found or inactive.",
        ["Sınava aday olarak yalnızca aday rolündeki kullanıcı atanabilir."] = "Only users with the candidate role can be assigned as candidates.",
        ["Sınava değerlendirici olarak yalnızca değerlendirici rolündeki kullanıcı atanabilir."] = "Only users with the reviewer role can be assigned as reviewers.",
        ["Kullanıcı bu sınava zaten atanmış."] = "The user is already assigned to this exam.",
        ["Aday bu sınava girmiş; ataması kaldırılamaz."] = "The candidate has taken this exam; the assignment cannot be removed.",
        ["Olay ağırlığı 0–100 arasında olmalıdır."] = "Event weight must be between 0 and 100.",
        ["Zamanla azalma 0–5 arasında olmalıdır."] = "Decay must be between 0 and 5.",
        ["Olay eşiği boş ya da 0–1000 arasında olmalıdır."] = "Event threshold must be empty or between 0 and 1000.",
        ["Oturum eşiği 1–1000 arasında olmalıdır."] = "Session threshold must be between 1 and 1000.",
        ["Aynı olay türü iki kez gönderilmiş."] = "The same event type was sent twice.",
        ["Bilinmeyen olay türü."] = "Unknown event type.",

        // ---- Oturum, sınav ----
        ["Sınav başlatılamıyor."] = "The exam cannot be started.",
        ["Sınavın erişim penceresi henüz açılmadı."] = "The exam's access window has not opened yet.",
        ["Sınava son başlangıç zamanı geçti."] = "The latest start time for this exam has passed.",
        ["Bu sınav için oturum hakkınızı kullandınız."] = "You have used your attempt for this exam.",
        ["Bu sınava atanmadınız."] = "You are not assigned to this exam.",
        ["Kayıt fotoğrafı yüklenmeden sınava başlanamaz."] = "You cannot start the exam before uploading a reference photo.",
        ["Oturum kapalı."] = "The session is closed.",
        ["Oturum bitti."] = "The session has ended.",
        ["Sınav süresi doldu; cevap kabul edilmedi."] = "Time is up; the answer was not accepted.",
        ["Soru bu sınava ait değil."] = "The question does not belong to this exam.",
        ["Seçenek geçersiz."] = "Invalid option.",
        ["Bu sıra numarası başka bir cevap için kullanılmış."] = "This sequence number was used for another answer.",
        ["Sınav zaten teslim edildi."] = "The exam has already been submitted.",
        ["Sınav henüz başlamadı."] = "The exam has not started yet.",
        ["Sınav sürmüyor."] = "The exam is not in progress.",
        ["Sınav süresi doldu."] = "Time is up.",
        ["Sınav sürmüyor; izleme verisi kabul edilmedi."] = "The exam is not in progress; monitoring data was not accepted.",
        ["Kare en fazla 1 MB olmalı."] = "A frame can be at most 1 MB.",
        ["Kareler en fazla 1 MB olmalı."] = "Frames can be at most 1 MB.",
        ["Kareler JPEG, PNG veya WebP olmalı."] = "Frames must be JPEG, PNG or WebP.",
        ["Ses parçası en fazla 256 KB olmalı."] = "An audio chunk can be at most 256 KB.",
        ["İznin verilip verilmediği belirtilmeli."] = "Whether permission was granted must be specified.",
        ["Bu olay türü tarayıcıdan gönderilemez."] = "This event type cannot be sent from the browser.",
        ["Olay zamanı gelecekte olamaz."] = "The event time cannot be in the future.",
        ["Güven skoru 0 ile 1 arasında olmalı."] = "Confidence must be between 0 and 1.",
        ["Ek bilgi bir JSON nesnesi veya dizisi olmalı."] = "Additional data must be a JSON object or array.",
        ["Ek bilgi geçerli bir JSON değil."] = "Additional data is not valid JSON.",

        // ---- Rıza, biyometri, doğrulama ----
        ["Biyometrik veri için açık rıza gerekli."] = "Explicit consent for biometric data is required.",
        ["Rıza metni güncel değil; sayfayı yenileyip tekrar onaylayın."] = "The consent text is out of date; reload the page and accept again.",
        ["Rızanın kabul edilip edilmediği belirtilmeli."] = "Whether consent was given must be specified.",
        ["Oturum rıza adımında değil."] = "The session is not at the consent step.",
        ["Fotoğraf JPEG, PNG veya WebP olmalı."] = "The photo must be JPEG, PNG or WebP.",
        ["Fotoğraf yok ya da 5 MB'tan büyük."] = "No photo, or the photo is larger than 5 MB.",
        ["Fotoğrafta tek ve net bir yüz bulunamadı."] = "No single, clear face was found in the photo.",
        ["Yüzünüzün açıkça göründüğü bir fotoğraf yükleyin."] = "Upload a photo where your face is clearly visible.",
        ["Fotoğrafta yalnızca siz olmalısınız."] = "Only you should be in the photo.",
        ["Yüzünüz fotoğrafta çok küçük; daha yakından çekilmiş bir fotoğraf yükleyin."] = "Your face is too small in the photo; upload a closer photo.",
        ["Fotoğraf bulanık; iyi ışıkta çekilmiş net bir fotoğraf yükleyin."] = "The photo is blurry; upload a sharp photo taken in good light.",
        ["Fotoğraf açılamadı; başka bir dosya deneyin."] = "The photo could not be opened; try another file.",
        ["Yüzünüzün önden ve net göründüğü, yalnızca sizin olduğunuz bir fotoğraf yükleyin."] = "Upload a photo where your face is seen clearly from the front and only you are in it.",
        ["Kayıt fotoğrafı bulunamadı."] = "Reference photo not found.",
        ["Kayıt fotoğrafını yalnızca adaylar yükleyebilir."] = "Only candidates can upload a reference photo.",
        ["Süren bir sınav oturumu varken kayıt fotoğrafı değiştirilemez."] = "The reference photo cannot be changed while an exam session is in progress.",
        ["Görüntü servisine şu an ulaşılamıyor."] = "The image service cannot be reached right now.",
        ["Görüntü servisine şu an ulaşılamıyor; biraz sonra tekrar deneyin."] = "The image service cannot be reached right now; try again shortly.",
        ["Oturum doğrulama adımında değil."] = "The session is not at the verification step.",
        ["Kimlik doğrulanamadı."] = "Identity could not be verified.",
        ["Canlı yüz algılanamadı."] = "No live face was detected.",
        ["Yeni denemeden önce birkaç saniye bekleyin."] = "Wait a few seconds before trying again.",

        // ---- Ajan ----
        ["Oturum ajan eşleştirme aşamasında değil."] = "The session is not at the agent pairing step.",
        ["Oturum ajan eşleştirme adımında değil."] = "The session is not at the agent pairing step.",
        ["Eşleştirme kodu geçersiz veya süresi dolmuş."] = "The pairing code is invalid or expired.",
        ["Geçersiz sinyal."] = "Invalid signal.",
        ["Sinyal doğrulanamadı."] = "The signal could not be verified.",
        ["Devam etmek için gerekçe yazın."] = "Write a justification to continue.",
        ["Güvenlik ajanı çalışıyor; ajansız devam edilemez."] = "The security agent is running; you cannot continue without it.",
        ["Ajanın bildirdiği bir gösterge yok."] = "The agent has not reported any indicator.",
        ["mode \"noAgent\" ya da \"indicated\" olmalı."] = "mode must be \"noAgent\" or \"indicated\".",

        // ---- İnceleme ----
        ["Karar Clean, Suspicious ya da Violation olmalı."] = "Decision must be Clean, Suspicious or Violation.",
        ["Bu oturum için zaten karar verildi."] = "A decision has already been made for this session.",
        ["Karar yalnızca riskli veya işaretli olarak tamamlanmış oturumlarda verilebilir."] = "A decision can only be made for sessions completed as risky or flagged.",
        ["Karar verilmiş oturumda yanlış alarm işaretlenemez."] = "A false alarm cannot be marked in a decided session.",
        ["Yanlış alarm yalnızca tamamlanmış oturumda işaretlenebilir."] = "A false alarm can only be marked in a completed session.",
        ["Olay zaten yanlış alarm olarak işaretli."] = "The event is already marked as a false alarm.",

        // ---- Platform ayarları ----
        ["Alternatif yöntem metni en fazla 4000 karakter olabilir."] = "The alternative method text can be at most 4000 characters.",
        ["Destek e-postası geçerli bir adres olmalıdır."] = "The support email must be a valid address.",
        ["Destek telefonu en fazla 40 karakter olabilir ve yalnızca rakam, boşluk, +, -, ( ) içerebilir."] =
            "The support phone can be at most 40 characters and contain only digits, spaces, +, -, ( ).",
        ["İndirme adresi https ile başlayan geçerli bir adres olmalıdır."] = "The download address must be a valid address starting with https.",
    };

    private static readonly (Regex Pattern, string English)[] Patterns =
    [
        (TooManyAttempts(), "Too many failed sign-in attempts. Try again in $1 minutes."),
        (FrameCount(), "Between $1 and $2 frames must be sent."),
        (ReasonLength(), "The justification can be at most $1 characters."),
        (PageSize(), "page must be at least 1 and pageSize 1–$1."),
        (PasswordMin(), "The password must be at least $1 characters."),
        (PasswordMax(), "The password can be at most $1 characters."),
        (Duration(), "Duration must be between 0 and $1 s."),
        (QuestionLength(), "Question text must be 1–$1 characters."),
        (OptionCount(), "A question must have 2–$1 options."),
        (OptionLength(), "Options cannot be empty and can be at most $1 characters."),
        (NoteLength(), "A note cannot be empty and can be at most $1 characters."),
    ];

    [GeneratedRegex(@"^Çok fazla hatalı giriş denemesi\. (\d+) dakika sonra tekrar deneyin\.$")]
    private static partial Regex TooManyAttempts();

    [GeneratedRegex(@"^(\d+) ile (\d+) arasında kare gönderilmeli\.$")]
    private static partial Regex FrameCount();

    [GeneratedRegex(@"^Gerekçe en fazla (\d+) karakter olabilir\.$")]
    private static partial Regex ReasonLength();

    [GeneratedRegex(@"^page en az 1, pageSize 1–(\d+) olmalı\.$")]
    private static partial Regex PageSize();

    [GeneratedRegex(@"^Şifre en az (\d+) karakter olmalıdır\.$")]
    private static partial Regex PasswordMin();

    [GeneratedRegex(@"^Şifre en fazla (\d+) karakter olabilir\.$")]
    private static partial Regex PasswordMax();

    [GeneratedRegex(@"^Süre 0 ile (\d+) sn arasında olmalı\.$")]
    private static partial Regex Duration();

    [GeneratedRegex(@"^Soru metni 1–(\d+) karakter olmalıdır\.$")]
    private static partial Regex QuestionLength();

    [GeneratedRegex(@"^Soruda 2–(\d+) seçenek olmalıdır\.$")]
    private static partial Regex OptionCount();

    [GeneratedRegex(@"^Seçenekler boş olamaz ve en fazla (\d+) karakter olabilir\.$")]
    private static partial Regex OptionLength();

    [GeneratedRegex(@"^Not boş olamaz ve en fazla (\d+) karakter olabilir\.$")]
    private static partial Regex NoteLength();

    /// <summary>Türkçe metnin İngilizcesi; karşılığı yoksa metnin kendisi (asla boş dönmez).</summary>
    public static string ToEnglish(string text)
    {
        if (Exact.TryGetValue(text, out var english))
        {
            return english;
        }

        foreach (var (pattern, replacement) in Patterns)
        {
            if (pattern.IsMatch(text))
            {
                return pattern.Replace(text, replacement);
            }
        }

        return text;
    }

    /// <summary>Çevirinin var olup olmadığı (kapsam testi için).</summary>
    public static bool IsKnown(string text) => Exact.ContainsKey(text) || Patterns.Any(p => p.Pattern.IsMatch(text));

    /// <summary>İstek İngilizceyse ProblemDetails'in başlık ve ayrıntısını çevirir.</summary>
    public static void Localize(ProblemDetails problem)
    {
        if (!ApiLanguage.IsEnglish)
        {
            return;
        }

        if (problem.Title is { } title) problem.Title = ToEnglish(title);
        if (problem.Detail is { } detail) problem.Detail = ToEnglish(detail);
    }
}
