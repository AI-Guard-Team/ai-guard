using AIGuard.Domain.Common;

namespace AIGuard.Domain.Entities;

/// <summary>Tarayıcı ile güvenlik ajanının eşleşmesi (Y1, Y2).</summary>
public class AgentPairing : Entity
{
    public Guid ExamSessionId { get; set; }

    public ExamSession ExamSession { get; set; } = null!;

    /// <summary>Tek kullanımlık eşleştirme kodunun SHA-256 özeti.</summary>
    public required string PairingCodeHash { get; set; }

    public DateTimeOffset PairingCodeExpiresAt { get; set; }

    public DateTimeOffset? PairedAt { get; set; }

    /// <summary>
    /// Sinyalleri imzalayan HMAC anahtarı, AES-GCM ile şifreli.
    /// İmzayı doğrulamak için anahtarın kendisi gerektiğinden özet yerine şifreli tutuluyor
    /// (K-08). Oturum bitince boşaltılmalı.
    /// </summary>
    public byte[]? EncryptedSessionKey { get; set; }

    /// <summary>30 saniye boyunca yeni sinyal gelmezse AGENT_SIGNAL_LOST üretiliyor.</summary>
    public DateTimeOffset? LastSignalAt { get; set; }

    public ICollection<AgentSignal> Signals { get; set; } = new List<AgentSignal>();
}

public class AgentSignal : Entity
{
    public Guid AgentPairingId { get; set; }

    public AgentPairing AgentPairing { get; set; } = null!;

    /// <summary>Ajanın imzaladığı zaman damgası.</summary>
    public DateTimeOffset SignedAt { get; set; }

    /// <summary>
    /// Tek kullanımlık değer. Uygulamadaki tekrar kontrolüne ek olarak (eşleşme, nonce)
    /// benzersiz indeksi var; API yeniden başlasa da aynı sinyal ikinci kez kaydedilemiyor.
    /// </summary>
    public required string Nonce { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Geçersiz imzalı sinyaller de kaydediliyor (AGENT_SIGNAL_INVALID).</summary>
    public bool SignatureValid { get; set; }

    /// <summary>Eşleşen göstergeler (jsonb). Tam süreç listesi gönderilmiyor (Y3).</summary>
    public string Indicators { get; set; } = "[]";
}
