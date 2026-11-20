using AIGuard.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Sessions;

/// <summary>
/// "Bu oturum bu adaya mı ait?" kontrolü (E2, S-9, S-12). Oturum uçları adayın JWT'sindeki
/// kimliği bununla karşılaştırıyor; aday yalnızca kendi oturumuna yazabiliyor.
/// </summary>
public sealed class SessionAccessService(IAppDbContext db)
{
    public Task<bool> IsCandidateOfAsync(Guid sessionId, Guid userId, CancellationToken ct = default) =>
        db.ExamSessions.AsNoTracking().AnyAsync(s => s.Id == sessionId && s.CandidateId == userId, ct);
}
