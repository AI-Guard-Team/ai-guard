namespace AIGuard.Domain.Enums;

public enum UserRole
{
    Admin,
    Reviewer,
    Candidate,
}

public enum ExamType
{
    GeneralAptitude,
    EnglishProficiency,
}

/// <summary>Kullanıcının sınava hangi rolle atandığı.</summary>
public enum AssignmentRole
{
    Candidate,
    Reviewer,
}

public enum EventSource
{
    Camera,
    Audio,
    Browser,
    Device,
    Network,
    System,
}

/// <summary>Kayıt fotoğrafı ve sınav izlemesi için ayrı rıza alınıyor (KVKK).</summary>
public enum ConsentType
{
    Biometric,
    Session,
}

/// <summary>Sınav başı doğrulama veya bağlantı kesintisinden dönüşte tekrar doğrulama.</summary>
public enum VerificationKind
{
    Initial,
    Reconnect,
}

/// <summary>Değerlendirici kararı: Temiz / Şüpheli / İhlal.</summary>
public enum ReviewDecisionType
{
    Clean,
    Suspicious,
    Violation,
}

/// <summary>
/// Oturum işaretleri; birden fazlası aynı anda olabilir.
/// Veritabanına sayı olarak yazıldığı için mevcut değerler değiştirilmemeli,
/// yeni işaret eklenirken bir sonraki bit kullanılmalı.
/// </summary>
[Flags]
public enum SessionFlags
{
    None = 0,

    /// <summary>Risk skoru eşiği aştı (A2).</summary>
    Risky = 1,

    /// <summary>Ajan gösterge buldu, aday gerekçe yazıp devam etti (Y4).</summary>
    Indicated = 2,

    /// <summary>Ajan çalışmadı, aday gerekçe yazıp devam etti (Y4).</summary>
    NoAgent = 4,

    /// <summary>Görüntü/ses servisi bir süre yanıt vermedi.</summary>
    MonitoringGap = 8,
}
