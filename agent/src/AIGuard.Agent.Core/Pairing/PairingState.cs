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
}
