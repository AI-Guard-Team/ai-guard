namespace AIGuard.Domain.Enums;

/// <summary>
/// Olay türleri. Görüntü servisi, ajan ve tarayıcıyla aynı adları kullandığımız için
/// C# adlandırma kuralı yerine kartlardaki yazım korundu (K-05).
/// </summary>
#pragma warning disable CA1707
public enum EventType
{
    // Kamera (D4)
    FACE_MISSING,
    MULTIPLE_FACES,
    GAZE_AWAY,
    PHONE,
    IDENTITY_MISMATCH,

    // Kimlik doğrulama (A3)
    VERIFICATION_FAILED,

    // Ses (D5)
    SPEECH,

    // Tarayıcı (S4)
    TAB_BLUR,
    FULLSCREEN_EXIT,
    COPY_PASTE,
    RIGHT_CLICK,
    PERMISSION_REVOKED,

    // Ajan (Y3, Y4, S6)
    VM_DETECTED,
    HYPERVISOR_INFO,
    FORBIDDEN_APP,
    VPN_PROXY,

    // Ağ (A2)
    IP_CHANGE,

    // Ajan sinyali (Y2)
    AGENT_SIGNAL_LOST,
    AGENT_SIGNAL_INVALID,

    // Sistem
    CONNECTION_LOST,
    MONITORING_GAP,
}
#pragma warning restore CA1707
