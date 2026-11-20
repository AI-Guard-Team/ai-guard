using AIGuard.Agent.Core.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AIGuard.Agent.Core.Pairing;

public sealed record LocalPairRequest(string? Code);

/// <summary>POST /pair { code }: tarayıcıdan gelen kodu sunucuya iletir (Y1).</summary>
internal static class PairingEndpoint
{
    // Sunucudaki kod 43 karakter; ajan biçimi gevşek kontrol ediyor ki sunucu kod uzunluğunu
    // değiştirirse ajan güncellemesi gerekmesin. Amaç yalnızca bozuk girdiyi ağa göndermemek.
    private const int MinCodeLength = 16;
    private const int MaxCodeLength = 128;

    public static async Task<IResult> HandleAsync(
        LocalPairRequest request,
        IPairingClient client,
        IDeviceInspector inspector,
        PairingState state,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger(typeof(PairingEndpoint));

        if (!IsPlausibleCode(request.Code))
        {
            return Results.BadRequest();
        }

        // İşletim sistemini tarayıcı değil ajan bildiriyor; platform modülü (Windows/MacOS) belirliyor.
        var outcome = await client.PairAsync(request.Code!, inspector.Platform, cancellationToken);

        if (outcome.Status == PairingStatus.Paired)
        {
            state.Set(outcome);
            logger.LogInformation("Eşleşme kuruldu (oturum {SessionId}).", outcome.SessionId);
        }

        // Tarayıcıya yalnızca durum dönüyor; sunucunun yanıt ayrıntısı ve eşleşme kimliği değil.
        return outcome.Status switch
        {
            PairingStatus.Paired => Results.Ok(new { status = "paired" }),
            PairingStatus.Rejected => Results.Json(new { status = "rejected" }, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status502BadGateway),
        };
    }

    internal static bool IsPlausibleCode(string? code) =>
        code is { Length: >= MinCodeLength and <= MaxCodeLength }
        && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
