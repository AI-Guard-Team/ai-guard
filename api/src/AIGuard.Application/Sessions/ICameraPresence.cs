namespace AIGuard.Application.Sessions;

/// <summary>
/// Oturumdan yakın zamanda kamera karesi gelip gelmediği (S-29). Sorular yalnızca kare gelen oturuma veriliyor:
/// önyüz soruları kamera açılmadan gizliyor, ama değiştirilmiş bir istemci kamera açmadan soruları sunucudan
/// okuyabiliyordu. Kural sunucuda olunca istemciye güvenilmemiş oluyor (GAB 3.3.2).
/// </summary>
public interface ICameraPresence
{
    bool HasRecentFrame(Guid sessionId, DateTimeOffset now);
}
