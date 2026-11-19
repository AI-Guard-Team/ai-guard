namespace AIGuard.Domain.Enums;

/// <summary>
/// Sınav oturumunun durumu. Adlar kartlardaki ortak adlarla aynı; geçiş kuralları E4'te.
/// Veritabanında metin olarak saklandığı için bir üyenin adını değiştirmek veri göçü gerektirir.
/// </summary>
public enum SessionState
{
    ConsentPending,
    AgentPairing,
    IdentityVerification,
    InProgress,
    PermissionPending,
    Disconnected,
    Completed,
    Cancelled,
    Decided,
}
