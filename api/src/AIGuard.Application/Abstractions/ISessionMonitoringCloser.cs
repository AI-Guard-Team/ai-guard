namespace AIGuard.Application.Abstractions;

/// <summary>
/// Oturum biterken (teslim, otomatik teslim) izleme tarafında açık kalan bölümleri yazar (D4, K-45).
/// </summary>
/// <remarks>
/// Kapalı oturuma olay yazılamadığı için (EventService) <see cref="Sessions.SessionService"/> bunu oturum satırı
/// kilitliyken, aynı işlem içinde ve durum <c>Completed</c>'a geçmeden önce çağırıyor; izinsiz sürenin
/// (PERMISSION_REVOKED) yazıldığı yerle aynı.
/// </remarks>
public interface ISessionMonitoringCloser
{
    Task CloseSessionAsync(Guid sessionId, CancellationToken ct = default);
}
