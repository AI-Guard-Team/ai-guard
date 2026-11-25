using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.ConsentTexts;

public sealed record ConsentTextView(ConsentType Type, string Version, string Title, string Body, DateTimeOffset PublishedAt);

/// <summary>
/// KVKK aydınlatma ve rıza metinleri (A5, K-32). Önyüz adaya güncel metni gösterip sürümünü rıza isteğiyle
/// geri gönderiyor; eski sürümler, adayın kabul ettiği metni göstermek için okunabiliyor.
/// </summary>
public sealed class ConsentTextService(IAppDbContext db)
{
    public Task<ConsentTextView?> GetCurrentAsync(ConsentType type, CancellationToken ct = default) =>
        Project(db.ConsentTexts.Current(type)).FirstOrDefaultAsync(ct);

    public Task<ConsentTextView?> GetAsync(ConsentType type, string version, CancellationToken ct = default) =>
        Project(db.ConsentTexts.Where(t => t.Type == type && t.Version == version)).FirstOrDefaultAsync(ct);

    private static IQueryable<ConsentTextView> Project(IQueryable<ConsentText> texts) =>
        texts.Select(t => new ConsentTextView(t.Type, t.Version, t.Title, t.Body, t.PublishedAt));
}

/// <summary>Rıza kaydeden servislerin (A3) güncel sürüm kontrolü.</summary>
public static class ConsentTextQueries
{
    /// <summary>Türün en son yayımlanan metni. Yeni sürüm eklenince eski sürümle verilen rıza isteği reddediliyor.</summary>
    public static IQueryable<ConsentText> Current(this IQueryable<ConsentText> texts, ConsentType type) =>
        texts.Where(t => t.Type == type).OrderByDescending(t => t.PublishedAt);

    public static async Task<bool> IsCurrentVersionAsync(
        this IAppDbContext db, ConsentType type, string? version, CancellationToken ct = default) =>
        version is not null
        && await db.ConsentTexts.Current(type).Select(t => t.Version).FirstOrDefaultAsync(ct) == version;
}
