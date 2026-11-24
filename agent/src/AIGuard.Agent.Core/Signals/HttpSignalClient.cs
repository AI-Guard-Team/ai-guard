using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace AIGuard.Agent.Core.Signals;

/// <summary>Sunucunun sinyale verdiği yanıtın ajan için anlamı (sunucu: AgentController, K-21).</summary>
public enum SignalSendResult
{
    /// <summary>202: imza ve zaman doğru, sinyal ve göstergeler kaydedildi.</summary>
    Accepted,

    /// <summary>Sunucu reddetti (401): imza, zaman ya da anahtar sorunu. Gönderim sürüyor.</summary>
    Rejected,

    /// <summary>Oturum bitti (410): anahtar geçersiz, gönderim durmalı.</summary>
    SessionEnded,

    /// <summary>Sunucuya ulaşılamadı ya da beklenmeyen yanıt.</summary>
    Failed,
}

public interface ISignalClient
{
    Task<SignalSendResult> SendAsync(SignalRequest signal, CancellationToken cancellationToken = default);
}

/// <summary>İmzalı sinyali <c>POST {ApiBaseUrl}/api/agent/signal</c> adresine TLS ile gönderir.</summary>
/// <remarks>
/// HttpClient'ın temel adresi ve zaman aşımı <see cref="AgentHost"/>'ta ayarlanıyor. Bu sınıf yalnızca isteği gönderip
/// durum kodunu <see cref="SignalSendResult"/>'a çeviriyor; ne yapılacağına <see cref="SignalSender"/> karar veriyor.
/// </remarks>
internal sealed class HttpSignalClient(HttpClient http, ILogger<HttpSignalClient> logger) : ISignalClient
{
    private const string SignalPath = "api/agent/signal";

    public async Task<SignalSendResult> SendAsync(SignalRequest signal, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(SignalPath, signal, cancellationToken);

            // Anahtar ve imza loglara yazılmıyor (kart); yalnızca durum kodu.
            switch (response.StatusCode)
            {
                case HttpStatusCode.Accepted:
                    return SignalSendResult.Accepted;
                case HttpStatusCode.Gone:
                    return SignalSendResult.SessionEnded;
                case HttpStatusCode.Unauthorized:
                    logger.LogWarning("Sunucu sinyali reddetti (401).");
                    return SignalSendResult.Rejected;
                default:
                    logger.LogWarning("Sunucu beklenmeyen yanıt verdi ({StatusCode}).", (int)response.StatusCode);
                    return SignalSendResult.Failed;
            }
        }
        // Ağ hatası ya da zaman aşımı "ulaşılamadı" sayılıyor. Ajan kapatılırken gelen iptal ise yukarı iletiliyor.
        catch (Exception ex) when (ex is HttpRequestException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Sinyal gönderilemedi.");
            return SignalSendResult.Failed;
        }
    }
}
