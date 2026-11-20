namespace AIGuard.Agent.Core.Pairing;

/// <summary>
/// Ajanın o anki eşleşmesi. Sinyal gönderimi (Y2) hangi eşleşme adına imza atacağını buradan okuyacak.
/// </summary>
public sealed class PairingState
{
    private PairingOutcome? _current;

    /// <summary>Henüz eşleşme yoksa null.</summary>
    public PairingOutcome? Current => Volatile.Read(ref _current);

    // Tarayıcı yeni kod gönderirse (ör. sayfa yenilendi) son başarılı eşleşme geçerli olur.
    internal void Set(PairingOutcome outcome) => Volatile.Write(ref _current, outcome);

    /// <summary>Oturum bitti (sunucu 410): anahtar unutulur, sinyal gönderimi durur.</summary>
    internal void Clear(PairingOutcome expected)
    {
        // Bu arada tarayıcı yeni bir eşleşme kurduysa onu silmiyoruz.
        if (Interlocked.CompareExchange(ref _current, null, expected) == expected && expected.SessionKey is { } key)
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }
}
