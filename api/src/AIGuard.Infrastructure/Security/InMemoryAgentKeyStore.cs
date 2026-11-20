using System.Collections.Concurrent;
using System.Security.Cryptography;
using AIGuard.Application.Abstractions;

namespace AIGuard.Infrastructure.Security;

/// <summary>Oturum anahtarları yalnızca bu süreçte, bellekte (K-21). Diske ve loga yazılmıyor.</summary>
public sealed class InMemoryAgentKeyStore : IAgentKeyStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _keys = new();

    public void Set(Guid sessionId, byte[] key)
    {
        // Kopya: çağıranın dizisi sonradan değiştirilse ya da silinse depodaki anahtar etkilenmesin.
        if (_keys.TryGetValue(sessionId, out var old))
        {
            CryptographicOperations.ZeroMemory(old);
        }

        _keys[sessionId] = key.ToArray();
    }

    public byte[]? Get(Guid sessionId) => _keys.TryGetValue(sessionId, out var key) ? key : null;

    public void Remove(Guid sessionId)
    {
        if (_keys.TryRemove(sessionId, out var key))
        {
            // Bellekte artık işe yaramaz bir kopya kalmasın.
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
