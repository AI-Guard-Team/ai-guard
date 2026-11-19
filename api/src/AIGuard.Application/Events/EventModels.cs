using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;

namespace AIGuard.Application.Events;

/// <summary>Tarayıcının gönderdiği olay (POST /api/sessions/{id}/events).</summary>
/// <remarks>
/// Kaynak ve başlangıç zamanı istemciden alınmıyor; sunucu kaynağı "browser",
/// başlangıcı da "alınma anı − süre" olarak belirliyor. Serbest metin/JSON alanı bilerek
/// yok: değerlendirici ekranında gösterilecek veriyi aday belirleyemesin, pano içeriği gibi
/// kişisel veri de yanlışlıkla kaydedilmesin (K-16).
/// </remarks>
public sealed record BrowserEventRequest(EventType Type, double DurationSec = 0);

/// <summary>Sunucu içinde üretilen olay: kamera/ses sonucu, ajan sinyali, IP değişimi vb.</summary>
public sealed record SystemEvent(
    EventType Type,
    DateTimeOffset StartedAt,
    double DurationSec = 0,
    double? Confidence = null,
    string? Payload = null);

public enum EventRecordStatus
{
    Recorded,
    SessionNotFound,
    SessionClosed,
    NotAllowedFromBrowser,
    RateLimited,
    Invalid,
}

public sealed record EventRecordResult(EventRecordStatus Status, Event? Event = null, string? Error = null)
{
    public static EventRecordResult Fail(EventRecordStatus status, string? error = null) => new(status, null, error);
}
