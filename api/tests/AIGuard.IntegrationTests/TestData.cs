using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;

namespace AIGuard.IntegrationTests;

/// <summary>Testler için en küçük geçerli kayıtları üretir.</summary>
internal static class TestData
{
    public static User Candidate(string? email = null)
    {
        email ??= $"{Guid.NewGuid():N}@test.local";
        return new User { FullName = "Test Aday", Email = email, NormalizedEmail = User.NormalizeEmail(email), Role = UserRole.Candidate };
    }

    public static User UserWithRole(UserRole role, string? email = null)
    {
        email ??= $"{Guid.NewGuid():N}@test.local";
        return new User { FullName = $"Test {role}", Email = email, NormalizedEmail = Domain.Entities.User.NormalizeEmail(email), Role = role };
    }

    public static Exam Exam() => new()
    {
        Name = "Test Sınavı",
        Type = ExamType.GeneralAptitude,
        DurationMinutes = 30,
        WindowStartAt = DateTimeOffset.UtcNow.AddHours(-1),
        WindowEndAt = DateTimeOffset.UtcNow.AddDays(1),
    };

    /// <summary>Kaydedilmiş bir aday + sınav + oturum döner.</summary>
    public static async Task<ExamSession> SavedSessionAsync(AppDbContext db)
    {
        var session = new ExamSession { Exam = Exam(), Candidate = Candidate() };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }
}
