using System.Security.Cryptography;
using AIGuard.Application.Agents;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Security;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Agents;

/// <summary>Eşleşmiş bir oturum: anahtar, bellek deposu ve servisler aynı saati kullanıyor.</summary>
internal sealed class PairedAgent
{
    public required Guid SessionId { get; init; }
    public required byte[] Key { get; init; }
    public required InMemoryAgentKeyStore Keys { get; init; }
    public required FakeTimeProvider Clock { get; init; }

    public AgentSignalService Signals(AppDbContext db) => new(db, Keys, Service(db, Clock), Clock);

    public AgentSignalMonitor Monitor(AppDbContext db) => new(db, Keys, Service(db, Clock), Clock);

    /// <summary>Ajanın göndereceği gibi imzalı sinyal; alanlar testte bozulabilsin diye ayrı ayrı verilebiliyor.</summary>
    public AgentSignalRequest Signal(string[]? indicators = null, long? ts = null, string? nonce = null, byte[]? key = null)
    {
        indicators ??= [];
        var t = ts ?? Clock.GetUtcNow().ToUnixTimeSeconds();
        var n = nonce ?? NewNonce();
        return new AgentSignalRequest(SessionId, t, n, indicators, AgentSignature.Sign(key ?? Key, SessionId, t, n, indicators));
    }

    public static string NewNonce() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>AgentPairing durumunda bir oturum açıp Y1 akışıyla eşleştirir.</summary>
    public static async Task<PairedAgent> CreateAsync(AppDbContext db, SessionState state = SessionState.AgentPairing)
    {
        var session = await InProgressSessionAsync(db, SessionState.AgentPairing);
        var clock = Clock();
        var keys = new InMemoryAgentKeyStore();
        var pairing = new AgentPairingService(db, clock, keys);
        var code = (await pairing.IssueCodeAsync(session.Id)).Code;
        var paired = await pairing.PairAsync(code, AgentPlatform.Windows);

        if (state != SessionState.AgentPairing)
        {
            session.State = state;
            await db.SaveChangesAsync();
        }

        return new PairedAgent { SessionId = session.Id, Key = paired.SessionKey!, Keys = keys, Clock = clock };
    }
}
