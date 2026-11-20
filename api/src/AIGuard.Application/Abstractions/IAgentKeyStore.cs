namespace AIGuard.Application.Abstractions;

/// <summary>
/// Ajan oturum anahtarlarının API belleğindeki deposu (Y2, K-21).
/// </summary>
/// <remarks>
/// GAB'a göre veritabanında yalnızca anahtarın özeti var; HMAC doğrulamak için anahtarın kendisi
/// gerektiğinden burada tutuluyor. API yeniden başlarsa anahtarlar kayboluyor ve o oturumların
/// sinyalleri doğrulanamıyor (AGENT_SIGNAL_LOST); bu bilinen bir sınırlılık.
/// </remarks>
public interface IAgentKeyStore
{
    void Set(Guid sessionId, byte[] key);

    byte[]? Get(Guid sessionId);

    /// <summary>Oturum bitince anahtar geçersiz kılınıyor (kart: FR-21).</summary>
    void Remove(Guid sessionId);
}
