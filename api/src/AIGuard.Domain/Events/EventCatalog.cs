using AIGuard.Domain.Enums;

namespace AIGuard.Domain.Events;

/// <summary>
/// Her olay türünün hangi kaynaktan geldiği ve tarayıcının hangi olayları
/// bildirebileceği. Kaynak istemciden alınmıyor, buradan belirleniyor (K-16).
/// </summary>
public static class EventCatalog
{
    private static readonly Dictionary<EventType, EventSource> Sources = new()
    {
        [EventType.FACE_MISSING] = EventSource.Camera,
        [EventType.MULTIPLE_FACES] = EventSource.Camera,
        [EventType.GAZE_AWAY] = EventSource.Camera,
        [EventType.PHONE] = EventSource.Camera,
        [EventType.IDENTITY_MISMATCH] = EventSource.Camera,
        [EventType.VERIFICATION_FAILED] = EventSource.Camera,
        [EventType.SPEECH] = EventSource.Audio,
        [EventType.TAB_BLUR] = EventSource.Browser,
        [EventType.FULLSCREEN_EXIT] = EventSource.Browser,
        [EventType.COPY_PASTE] = EventSource.Browser,
        [EventType.RIGHT_CLICK] = EventSource.Browser,
        [EventType.PERMISSION_REVOKED] = EventSource.Browser,
        [EventType.VM_DETECTED] = EventSource.Device,
        [EventType.HYPERVISOR_INFO] = EventSource.Device,
        [EventType.FORBIDDEN_APP] = EventSource.Device,
        [EventType.VPN_PROXY] = EventSource.Network,
        [EventType.IP_CHANGE] = EventSource.Network,
        [EventType.AGENT_SIGNAL_LOST] = EventSource.System,
        [EventType.AGENT_SIGNAL_INVALID] = EventSource.System,
        [EventType.CONNECTION_LOST] = EventSource.System,
        [EventType.MONITORING_GAP] = EventSource.System,
    };

    public static EventSource SourceOf(EventType type) => Sources[type];

    /// <summary>
    /// Tarayıcının doğrudan bildirebileceği olaylar. Kamera, ses, ajan ve ağ olayları
    /// sunucu tarafında üretiliyor; tarayıcı bunları gönderemiyor.
    /// </summary>
    public static bool IsBrowserReportable(EventType type) => SourceOf(type) == EventSource.Browser;
}
