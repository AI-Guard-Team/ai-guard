using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Exams;

/// <summary>Sınava aday ve değerlendirici atama (E3, FR-08).</summary>
public sealed class AssignmentService(IAppDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<AssignmentDetail>?> ListAsync(Guid examId, CancellationToken ct = default)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return null;
        }

        return await db.ExamAssignments.AsNoTracking()
            .Where(a => a.ExamId == examId)
            .OrderBy(a => a.Role).ThenBy(a => a.User.FullName)
            .Select(a => new AssignmentDetail(a.Id, a.UserId, a.User.FullName, a.User.Email, a.Role, a.AssignedAt, a.AttemptUsedAt))
            .ToListAsync(ct);
    }

    public async Task<ExamOperationResult> AssignAsync(Guid examId, AssignmentRequest request, CancellationToken ct = default)
    {
        if (request.UserId is not { } userId || request.Role is not { } role || !Enum.IsDefined(role))
        {
            return ExamOperationResult.Invalid("Kullanıcı ve rol (Candidate veya Reviewer) zorunludur.");
        }

        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return ExamOperationResult.NotFound;
        }

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            return ExamOperationResult.Invalid("Kullanıcı bulunamadı veya pasif.");
        }

        // Atama rolü kullanıcının rolüyle aynı olmalı: aday hesabı değerlendirici olarak atanıp başka
        // adayların kanıtlarını göremesin (K-23).
        var expected = role == AssignmentRole.Candidate ? UserRole.Candidate : UserRole.Reviewer;
        if (user.Role != expected)
        {
            return ExamOperationResult.Invalid(role == AssignmentRole.Candidate
                ? "Sınava aday olarak yalnızca aday rolündeki kullanıcı atanabilir."
                : "Sınava değerlendirici olarak yalnızca değerlendirici rolündeki kullanıcı atanabilir.");
        }

        var assignment = new ExamAssignment { ExamId = examId, UserId = userId, Role = role, AssignedAt = clock.GetUtcNow() };
        db.ExamAssignments.Add(assignment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (db.IsUniqueViolation(ex))
        {
            // (exam_id, user_id) benzersiz (A1): kullanıcı bu sınava zaten atanmış.
            return ExamOperationResult.Conflict("Kullanıcı bu sınava zaten atanmış.");
        }

        return ExamOperationResult.Ok(assignment.Id);
    }

    public async Task<ExamOperationResult> RemoveAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await db.ExamAssignments.SingleOrDefaultAsync(a => a.Id == assignmentId, ct);
        if (assignment is null)
        {
            return ExamOperationResult.NotFound;
        }

        // Sınava girmiş adayın ataması kalkarsa oturumu sahipsiz görünür ve değerlendirme bozulur.
        if (assignment.Role == AssignmentRole.Candidate
            && (assignment.AttemptUsedAt is not null
                || await db.ExamSessions.AnyAsync(s => s.ExamId == assignment.ExamId && s.CandidateId == assignment.UserId, ct)))
        {
            return ExamOperationResult.Conflict("Aday bu sınava girmiş; ataması kaldırılamaz.");
        }

        db.ExamAssignments.Remove(assignment);
        await db.SaveChangesAsync(ct);
        return ExamOperationResult.Ok();
    }
}
