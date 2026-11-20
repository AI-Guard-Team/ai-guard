using AIGuard.Application.Agents;
using AIGuard.Infrastructure.Security;

namespace AIGuard.UnitTests.Agents;

public class AgentSignatureTests
{
    // Sabit örnek. Beklenen imzalar bağımsız olarak (Python hmac) hesaplandı. Ajandaki
    // SignalSignerTests aynı değerleri bekliyor: iki taraf biçimde ayrışırsa ikisinden biri kırılır.
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly Guid SessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const long Ts = 1795000000;
    private const string Nonce = "AAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void Known_answer_with_indicators()
    {
        Assert.Equal(
            """11111111-2222-3333-4444-555555555555|1795000000|AAAAAAAAAAAAAAAAAAAAAA|["VM_DETECTED","ANYDESK"]""",
            AgentSignature.CanonicalMessage(SessionId, Ts, Nonce, ["VM_DETECTED", "ANYDESK"]));
        Assert.Equal("J1SHo9DqrRaH0vnJ79Vwnegs1h17ovczMFhMHML4ZNA=", AgentSignature.Sign(Key, SessionId, Ts, Nonce, ["VM_DETECTED", "ANYDESK"]));
    }

    [Fact]
    public void Known_answer_without_indicators()
    {
        Assert.Equal("OeLgVDkYiPEcpOeOR3+aaKrlOZT1kcCMsuhFI2y7aUU=", AgentSignature.Sign(Key, SessionId, Ts, Nonce, []));
    }

    [Fact]
    public void Key_hash_is_sha256_hex_and_fits_the_column()
    {
        Assert.Equal("630DCD2966C4336691125448BBB25B4FF412A49C732DB2C8ABC1B8581BD710DD", AgentSignature.HashKey(Key));
    }

    [Fact]
    public void Generated_keys_are_32_random_bytes()
    {
        var a = AgentSignature.GenerateKey();
        Assert.Equal(32, a.Length);
        Assert.NotEqual(a, AgentSignature.GenerateKey());
    }

    [Fact]
    public void Valid_signature_verifies()
    {
        var sig = AgentSignature.Sign(Key, SessionId, Ts, Nonce, ["VM_DETECTED"]);

        Assert.True(AgentSignature.Verify(Key, SessionId, Ts, Nonce, ["VM_DETECTED"], sig));
    }

    public static TheoryData<string> TamperedFields => new() { "session", "ts", "nonce", "indicators", "key" };

    [Theory]
    [MemberData(nameof(TamperedFields))]
    public void Changing_any_signed_field_breaks_the_signature(string field)
    {
        var sig = AgentSignature.Sign(Key, SessionId, Ts, Nonce, ["VM_DETECTED"]);
        var key = field == "key" ? AgentSignature.GenerateKey() : Key;

        var ok = AgentSignature.Verify(
            key,
            field == "session" ? Guid.NewGuid() : SessionId,
            field == "ts" ? Ts + 1 : Ts,
            field == "nonce" ? "BBBBBBBBBBBBBBBBBBBBBB" : Nonce,
            field == "indicators" ? [] : ["VM_DETECTED"],
            sig);

        Assert.False(ok);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("base64-degil!")]
    [InlineData("AAAA")] // geçerli Base64 ama 32 bayt değil
    public void Malformed_signatures_are_rejected_without_exception(string? sig)
    {
        Assert.False(AgentSignature.Verify(Key, SessionId, Ts, Nonce, [], sig));
    }

    [Fact]
    public void Key_store_keeps_its_own_copy_and_forgets_on_remove()
    {
        var store = new InMemoryAgentKeyStore();
        var key = AgentSignature.GenerateKey();
        var original = key.ToArray();

        store.Set(SessionId, key);
        Array.Clear(key);

        Assert.Equal(original, store.Get(SessionId));
        store.Remove(SessionId);
        Assert.Null(store.Get(SessionId));
    }
}
