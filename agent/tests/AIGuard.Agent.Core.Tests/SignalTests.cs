using System.Net;
using System.Text;
using AIGuard.Agent.Core.Pairing;
using AIGuard.Agent.Core.Platform;
using AIGuard.Agent.Core.Signals;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AIGuard.Agent.Core.Tests;

public class SignalSignerTests
{
    // Sunucudaki AgentSignatureTests ile aynı sabit örnek ve aynı beklenen imzalar (Python hmac ile
    // bağımsız hesaplandı). Biri değişirse diğeri de değişmeli; yoksa sunucu bütün sinyalleri reddeder.
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly Guid SessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Known_answer_matches_the_server()
    {
        DeviceIndicator[] sample = [new("VM_DETECTED", "VirtualBox", "wmi:model"), new("FORBIDDEN_APP", "AnyDesk")];

        Assert.Equal(
            """11111111-2222-3333-4444-555555555555|1795000000|AAAAAAAAAAAAAAAAAAAAAA|[{"type":"VM_DETECTED","name":"VirtualBox","detail":"wmi:model"},{"type":"FORBIDDEN_APP","name":"AnyDesk","detail":null}]""",
            SignalSigner.CanonicalMessage(SessionId, 1795000000, "AAAAAAAAAAAAAAAAAAAAAA", sample));
        Assert.Equal("Uu1Z9ZVrLmt7t5lQIIkbxZSHpfqRXmh2aJy3Igh88DU=",
            SignalSigner.Sign(Key, SessionId, 1795000000, "AAAAAAAAAAAAAAAAAAAAAA", sample));
        Assert.Equal("OeLgVDkYiPEcpOeOR3+aaKrlOZT1kcCMsuhFI2y7aUU=",
            SignalSigner.Sign(Key, SessionId, 1795000000, "AAAAAAAAAAAAAAAAAAAAAA", []));
    }

    [Fact]
    public void Each_signal_gets_a_fresh_nonce_and_the_current_time()
    {
        var now = new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

        var a = SignalSigner.Create(Key, SessionId, now, []);
        var b = SignalSigner.Create(Key, SessionId, now, []);

        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.Equal(now.ToUnixTimeSeconds(), a.Ts);
        Assert.Equal(SignalSigner.Sign(Key, SessionId, a.Ts, a.Nonce, []), a.Sig);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", a.Nonce);
    }
}

public class SignalSenderTests
{
    private sealed class FakeSignalClient : ISignalClient
    {
        public SignalSendResult Result { get; set; } = SignalSendResult.Accepted;
        public List<SignalRequest> Sent { get; } = [];

        public Task<SignalSendResult> SendAsync(SignalRequest signal, CancellationToken cancellationToken = default)
        {
            Sent.Add(signal);
            return Task.FromResult(Result);
        }
    }

    private sealed class Inspector(Func<IReadOnlyList<DeviceIndicator>> inspect) : IDeviceInspector
    {
        public string Platform => "Windows";
        public Task<IReadOnlyList<DeviceIndicator>> InspectAsync(CancellationToken cancellationToken = default) => Task.FromResult(inspect());
    }

    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private static (SignalSender Sender, PairingState State, FakeSignalClient Client) Create(
        bool paired = true, Func<IReadOnlyList<DeviceIndicator>>? inspect = null)
    {
        var state = new PairingState();
        if (paired)
        {
            state.Set(new PairingOutcome(PairingStatus.Paired, Guid.NewGuid(), Guid.NewGuid(), Key.ToArray()));
        }

        var client = new FakeSignalClient();
        var sender = new SignalSender(state, new Inspector(inspect ?? (() => [])), client, TimeProvider.System,
            Options.Create(new AgentOptions()), NullLogger<SignalSender>.Instance);
        return (sender, state, client);
    }

    [Fact]
    public async Task Nothing_is_sent_before_pairing()
    {
        var (sender, _, client) = Create(paired: false);

        Assert.Null(await sender.SendOnceAsync());
        Assert.Empty(client.Sent);
    }

    [Fact]
    public async Task Signal_carries_the_indicators_and_is_signed_with_the_session_key()
    {
        DeviceIndicator vm = new("VM_DETECTED", "VirtualBox", "wmi");
        var (sender, state, client) = Create(inspect: () => [vm]);

        await sender.SendOnceAsync();

        var s = Assert.Single(client.Sent);
        Assert.Equal(state.Current!.SessionId, s.SessionId);
        Assert.Equal([vm], s.Indicators);
        Assert.Equal(SignalSigner.Sign(Key, s.SessionId, s.Ts, s.Nonce, s.Indicators), s.Sig);
    }

    [Fact]
    public async Task Session_end_clears_the_key_and_stops_sending()
    {
        var (sender, state, client) = Create();
        client.Result = SignalSendResult.SessionEnded;

        await sender.SendOnceAsync();
        var afterEnd = await sender.SendOnceAsync();

        Assert.Null(state.Current);
        Assert.Null(afterEnd);
        Assert.Single(client.Sent);
    }

    [Fact]
    public async Task Rejected_signal_does_not_stop_sending()
    {
        // 401 geçici olabilir (ör. saat kayması); durursak sunucu "sinyal kesildi" yazar.
        var (sender, state, client) = Create();
        client.Result = SignalSendResult.Rejected;

        await sender.SendOnceAsync();

        Assert.NotNull(state.Current);
    }

    [Fact]
    public async Task Inspection_failure_still_sends_a_signal()
    {
        var (sender, _, client) = Create(inspect: () => throw new InvalidOperationException("WMI erişilemedi"));

        await sender.SendOnceAsync();

        Assert.Empty(Assert.Single(client.Sent).Indicators);
    }
}

public class HttpSignalClientTests
{
    private sealed class Stub(HttpStatusCode status) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, SignalSendResult.Accepted)]
    [InlineData(HttpStatusCode.Unauthorized, SignalSendResult.Rejected)]
    [InlineData(HttpStatusCode.Gone, SignalSendResult.SessionEnded)]
    [InlineData(HttpStatusCode.InternalServerError, SignalSendResult.Failed)]
    public async Task Server_status_is_mapped_and_body_has_the_card_fields(HttpStatusCode status, SignalSendResult expected)
    {
        var stub = new Stub(status);
        var client = new HttpSignalClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.ornek.edu.tr/") },
            NullLogger<HttpSignalClient>.Instance);
        var signal = new SignalRequest(Guid.Empty, 1795000000, "AAAAAAAAAAAAAAAAAAAAAA",
            [new DeviceIndicator("FORBIDDEN_APP", "AnyDesk", "AnyDesk.exe")], "c2ln");

        var result = await client.SendAsync(signal);

        Assert.Equal(expected, result);
        Assert.Equal("/api/agent/signal", stub.Path);
        Assert.Equal("""{"sessionId":"00000000-0000-0000-0000-000000000000","ts":1795000000,"nonce":"AAAAAAAAAAAAAAAAAAAAAA","indicators":[{"type":"FORBIDDEN_APP","name":"AnyDesk","detail":"AnyDesk.exe"}],"sig":"c2ln"}""", stub.Body);
    }
}

public class PairingKeyTests
{
    private sealed class Stub(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private static Task<PairingOutcome> PairAsync(string json) =>
        new HttpPairingClient(new HttpClient(new Stub(json)) { BaseAddress = new Uri("https://api.ornek.edu.tr/") },
            NullLogger<HttpPairingClient>.Instance).PairAsync("kod", "Windows");

    [Fact]
    public async Task Session_key_from_the_pair_response_is_kept_in_memory()
    {
        var key = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

        var outcome = await PairAsync($$"""{"pairingId":"{{Guid.NewGuid()}}","sessionId":"{{Guid.NewGuid()}}","sessionKey":"{{key}}"}""");

        Assert.Equal(PairingStatus.Paired, outcome.Status);
        Assert.Equal(32, outcome.SessionKey!.Length);
    }

    [Theory]
    [InlineData(",\"sessionKey\":\"AAAA\"")] // 32 bayt değil
    [InlineData("")]                          // anahtar yok
    public async Task Pairing_without_a_valid_key_is_not_accepted(string keyField)
    {
        var outcome = await PairAsync($$"""{"pairingId":"{{Guid.NewGuid()}}","sessionId":"{{Guid.NewGuid()}}"{{keyField}}}""");

        Assert.Equal(PairingStatus.Unavailable, outcome.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void Signal_interval_must_stay_well_below_thirty_seconds(int seconds)
    {
        var options = new AgentOptions
        {
            ApiBaseUrl = new Uri("https://api.ornek.edu.tr"),
            AllowedOrigins = ["https://sinav.ornek.edu.tr"],
            SignalIntervalSeconds = seconds,
        };

        Assert.False(new AgentOptionsValidator().Validate(null, options).Succeeded);
    }
}
