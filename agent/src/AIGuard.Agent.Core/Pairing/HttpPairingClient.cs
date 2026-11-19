using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIGuard.Agent.Core.Pairing;

/// <summary>
/// Kodu POST {ApiBaseUrl}/api/agent/pair adresine iletir.
/// </summary>
/// <remarks>
/// https zorunluluğu <see cref="AgentOptionsValidator"/>'da; sertifika doğrulaması .NET'in
/// varsayılanı ve bilerek kapatılmıyor (kapatılırsa araya giren biri kodu okuyabilir).
/// </remarks>
internal sealed class HttpPairingClient(HttpClient http, ILogger<HttpPairingClient> logger) : IPairingClient
{
    // Sunucu adresinin sonunda yol olabilir (https://ornek.edu.tr/aiguard/); göreli adres onun altına eklenir.
    private const string PairPath = "api/agent/pair";

    private sealed record PairResponse(Guid PairingId, Guid SessionId);

    public async Task<PairingOutcome> PairAsync(string code, string os, CancellationToken cancellationToken = default)
    {
        try
        {
            // Kart: POST /api/agent/pair { code, os }. Sunucu işletim sistemini AgentPairing.Platform'a yazıyor.
            using var response = await http.PostAsJsonAsync(PairPath, new { code, os }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<PairResponse>(cancellationToken); // varsayılan: web (camelCase) ayarları
                return body is null
                    ? new(PairingStatus.Unavailable)
                    : new(PairingStatus.Paired, body.PairingId, body.SessionId);
            }

            // 4xx: sunucu kodu reddetti. Kodun kendisi loglanmıyor; loglar bir kimlik bilgisi taşımamalı.
            if ((int)response.StatusCode is >= 400 and < 500)
            {
                logger.LogWarning("Sunucu eşleştirmeyi reddetti ({StatusCode}).", (int)response.StatusCode);
                return new(PairingStatus.Rejected);
            }

            logger.LogWarning("Sunucu beklenmeyen yanıt verdi ({StatusCode}).", (int)response.StatusCode);
            return new(PairingStatus.Unavailable);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // TLS/sertifika hatası da HttpRequestException olarak geliyor. Tarayıcı çağrısı iptal
            // edilmediyse TaskCanceledException zaman aşımı demek.
            logger.LogWarning(ex, "Sunucuya ulaşılamadı.");
            return new(PairingStatus.Unavailable);
        }
    }
}
