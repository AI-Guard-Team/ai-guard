namespace AIGuard.Domain.Common;

/// <summary>Tüm varlıkların ortak tabanı.</summary>
public abstract class Entity
{
    // Kimlikler URL'de göründüğü için tahmin edilemeyen Guid kullanıyoruz (K-02).
    public Guid Id { get; set; } = Guid.NewGuid();
}
