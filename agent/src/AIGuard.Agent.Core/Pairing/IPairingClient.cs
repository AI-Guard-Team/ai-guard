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
    Task<PairingOutcome> PairAsync(string code, CancellationToken cancellationToken = default);
}
