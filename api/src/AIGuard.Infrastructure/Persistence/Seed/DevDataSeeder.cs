using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIGuard.Infrastructure.Persistence.Seed;

/// <summary>
/// Geliştirme ortamı için örnek veri: 1 yönetici, 1 değerlendirici, 2 aday ve 1 sınav.
/// Göçe konmadı, yalnızca Development'ta açılışta yükleniyor (K-06). Veritabanında
/// kullanıcı varsa hiçbir şey yapmıyor.
/// </summary>
/// <remarks>
/// Şifre Seed:DefaultPassword ayarından okunuyor; ayar yoksa kullanıcılar şifresiz oluşuyor
/// ve giriş yapılamıyor.
/// </remarks>
public sealed class DevDataSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<DevDataSeeder> logger)
{
    public const int MinimumSeedPasswordLength = 12;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Program.cs zaten kontrol ediyor; yanlışlıkla başka yerden çağrılırsa diye burada da.
        if (!environment.IsDevelopment())
        {
            logger.LogWarning("DevDataSeeder yalnızca Development ortamında çalışır; atlandı.");
            return;
        }

        // Göçler uygulanmamışsa anlaşılır bir uyarı verip çıkıyoruz.
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            logger.LogWarning("Uygulanmamış göçler var; örnek veri yüklenmedi. Önce 'dotnet ef database update' çalıştırın.");
            return;
        }

        if (await db.Users.AnyAsync(cancellationToken))
        {
            return; // zaten yüklü
        }

        var password = configuration["Seed:DefaultPassword"];
        if (password is not null && password.Length < MinimumSeedPasswordLength)
        {
            throw new InvalidOperationException(
                $"Seed:DefaultPassword en az {MinimumSeedPasswordLength} karakter olmalı.");
        }

        var now = DateTimeOffset.UtcNow;

        User CreateUser(string name, string email, UserRole role)
        {
            var user = new User
            {
                FullName = name,
                Email = email,
                NormalizedEmail = User.NormalizeEmail(email),
                Role = role,
            };
            if (password is not null)
            {
                user.PasswordHash = passwordHasher.Hash(password);
                user.InviteAcceptedAt = now;
            }
            return user;
        }

        // .local adresleri gerçek bir e-postaya karşılık gelmiyor.
        var admin = CreateUser("Örnek Yönetici", "admin@aiguard.local", UserRole.Admin);
        var reviewer = CreateUser("Örnek Değerlendirici", "reviewer@aiguard.local", UserRole.Reviewer);
        var candidate1 = CreateUser("Örnek Aday 1", "aday1@aiguard.local", UserRole.Candidate);
        var candidate2 = CreateUser("Örnek Aday 2", "aday2@aiguard.local", UserRole.Candidate);

        var exam = new Exam
        {
            Name = "Örnek Genel Yetenek Sınavı",
            Type = ExamType.GeneralAptitude,
            DurationMinutes = 30,
            // Pencere şu an açık, böylece hemen denenebilir.
            WindowStartAt = now.AddDays(-1),
            WindowEndAt = now.AddDays(7),
            RetentionDays = 30,
            Questions =
            {
                new Question { Order = 1, Text = "2, 4, 8, 16, ... dizisinde sıradaki sayı hangisidir?", Options = ["24", "32", "30", "64"], CorrectOptionIndex = 1 },
                new Question { Order = 2, Text = "Tüm kediler hayvandır. Tekir bir kedidir. Buna göre hangisi kesinlikle doğrudur?", Options = ["Tekir bir hayvandır", "Tüm hayvanlar kedidir", "Tekir bir köpektir"], CorrectOptionIndex = 0 },
                new Question { Order = 3, Text = "Choose the correct option: She ___ to school every day.", Options = ["go", "goes", "going", "gone"], CorrectOptionIndex = 1 },
            },
        };

        exam.Assignments.Add(new ExamAssignment { User = reviewer, Role = AssignmentRole.Reviewer });
        exam.Assignments.Add(new ExamAssignment { User = candidate1, Role = AssignmentRole.Candidate });
        exam.Assignments.Add(new ExamAssignment { User = candidate2, Role = AssignmentRole.Candidate });

        db.Users.AddRange(admin, reviewer, candidate1, candidate2);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Örnek veri yüklendi (4 kullanıcı, 1 sınav). Şifre {PasswordState}.",
            password is null ? "TANIMLI DEĞİL: kullanıcılar giriş yapamaz" : "Seed:DefaultPassword değerinden");
    }
}
