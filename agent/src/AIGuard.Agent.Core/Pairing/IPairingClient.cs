namespace AIGuard.Agent.Core.Pairing;

public enum PairingStatus
{
    Paired,

    /// <summary>Sunucu kodu reddetti (yanlış, süresi dolmuş veya kullanılmış).</summary>
    Rejected,

    /// <summary>Sunucuya ulaşılamadı, TLS hatası, zaman aşımı veya beklenmeyen yanıt.</summary>
    Unavailable,
}

public sealed record PairingOutcome(PairingStatus Status, Guid? PairingId = null, Guid? SessionId = null);

/// <summary>Eşleştirme kodunu sunucuya ileten istemci.</summary>
public interface IPairingClient
{
    /// <param name="code">Tarayıcının gönderdiği tek kullanımlık kod.</param>
    /// <param name="os">Sunucunun beklediği işletim sistemi adı: "Windows" veya "MacOS".</param>
    Task<PairingOutcome> PairAsync(string code, string os, CancellationToken cancellationToken = default);
}
