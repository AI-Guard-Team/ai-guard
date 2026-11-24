using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Application.Audit;
using AIGuard.Application.ConsentTexts;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.IntegrationTests.Review;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Review.ReviewTestHelpers;

namespace AIGuard.IntegrationTests.DataProtection;

/// <summary>A5 uçları: rıza metinleri, denetim kaydının görüntülenmesi ve kişisel veri ekranlarının kaydı.</summary>
public class DataProtectionApiTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [SkippableFact]
    public async Task Consent_texts_are_readable_by_signed_in_users_by_type_and_version()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var candidate = TestData.Candidate();
        await using (var db = fx.CreateContext())
        {
            db.Users.Add(candidate);
            await db.SaveChangesAsync();
        }

        var api = new TestApi(fx);
        var client = api.As(candidate);

        var current = await client.GetFromJsonAsync<ConsentTextView>("/api/consent-texts/Biometric", Json);
        Assert.Equal(ConsentText.InitialBiometricVersion, current?.Version);
        Assert.False(string.IsNullOrWhiteSpace(current?.Body));

        var session = await client.GetFromJsonAsync<ConsentTextView>($"/api/consent-texts/Session/{ConsentText.InitialSessionVersion}", Json);
        Assert.Equal(ConsentType.Session, session?.Type);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/consent-texts/Biometric/kvkk-biometric-v99")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/consent-texts/Unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync("/api/consent-texts/Biometric")).StatusCode);
    }

    [SkippableFact]
    public async Task Only_admin_can_read_the_audit_log_with_filters_and_paging()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var admin = TestData.UserWithRole(UserRole.Admin);
        var reviewer = TestData.UserWithRole(UserRole.Reviewer);
        var candidate = TestData.Candidate();
        var prefix = $"test{Guid.NewGuid():N}.";
        await using (var db = fx.CreateContext())
        {
            db.Users.AddRange(admin, reviewer, candidate);
            db.AuditLogs.AddRange(
                new AuditLog { Action = prefix + "first", ActorUser = admin, OccurredAt = ExamStart },
                new AuditLog { Action = prefix + "second", OccurredAt = ExamStart.AddMinutes(1) },
                new AuditLog { Action = prefix + "third", OccurredAt = ExamStart.AddMinutes(2) });
            await db.SaveChangesAsync();
        }

        var api = new TestApi(fx);
        var url = $"/api/audit-logs?action={prefix}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.As(reviewer).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.As(candidate).GetAsync(url)).StatusCode);

        var client = api.As(admin);
        var all = await client.GetFromJsonAsync<AuditLogPage>(url, Json);
        Assert.Equal(3, all!.Total);
        Assert.Equal([prefix + "third", prefix + "second", prefix + "first"], all.Items.Select(i => i.Action));
        Assert.Equal(admin.FullName, all.Items[2].ActorName);

        var page = await client.GetFromJsonAsync<AuditLogPage>($"{url}&page=2&pageSize=1", Json);
        Assert.Equal(prefix + "second", Assert.Single(page!.Items).Action);

        var from = Uri.EscapeDataString(ExamStart.AddSeconds(30).ToString("O"));
        var ranged = await client.GetFromJsonAsync<AuditLogPage>($"{url}&from={from}", Json);
        Assert.Equal(2, ranged!.Total);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{url}&pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{url}&pageSize={AuditLogService.MaxPageSize + 1}")).StatusCode);
        var to = Uri.EscapeDataString(ExamStart.AddDays(-1).ToString("O"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{url}&from={from}&to={to}")).StatusCode);

        // Yalnızca okuma: yazma yöntemi yok.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync("/api/audit-logs")).StatusCode);
    }

    [SkippableFact]
    public async Task Report_downloads_as_excel_or_pdf_with_the_same_access_rules()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        User candidate;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, "Aday", new EventSpec(EventType.PHONE, 10, 5));
            candidate = await db.Users.AsNoTracking().SingleAsync(u => u.Id == session.CandidateId);
        }

        var api = new TestApi(fx);
        var url = $"/api/reports/exams/{s.Exam.Id}/sessions/{session.Id}";

        var xlsx = await api.As(s.Reviewer).GetAsync($"{url}?format=Xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"aday-raporu-{session.Id:N}.xlsx", xlsx.Content.Headers.ContentDisposition?.FileNameStar ?? xlsx.Content.Headers.ContentDisposition?.FileName);
        Assert.True(xlsx.Headers.CacheControl?.NoStore);

        var pdf = await api.As(s.Admin).GetAsync($"{url}?format=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..5]));

        // Erişim kuralı JSON raporla aynı.
        Assert.Equal(HttpStatusCode.NotFound, (await api.As(s.OtherReviewer).GetAsync($"{url}?format=Pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.As(candidate).GetAsync($"{url}?format=Xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.As(s.Reviewer).GetAsync($"{url}?format=Docx")).StatusCode);

        // Dosya sistemin dışına çıktığı için görüntülemeden ayrı kaydediliyor.
        await using var check = fx.CreateContext();
        var exports = await check.AuditLogs.AsNoTracking()
            .Where(a => a.TargetId == session.Id && a.Action == "report.export").ToListAsync();
        Assert.Equal(2, exports.Count);
        Assert.Contains(exports, a => a.ActorUserId == s.Reviewer.Id && a.NewValue!.Contains("Xlsx"));
        Assert.Contains(exports, a => a.ActorUserId == s.Admin.Id && a.NewValue!.Contains("Pdf"));
    }

    [SkippableFact]
    public async Task Opening_review_detail_or_report_is_recorded_with_who_looked()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        ReviewScenario s;
        ExamSession session;
        await using (var db = fx.CreateContext())
        {
            s = await ScenarioAsync(db);
            session = await SessionAsync(db, s.Exam, SessionState.Completed, SessionFlags.Risky, "Aday",
                new EventSpec(EventType.PHONE, 10, 5, WithEvidence: true));
        }

        var api = new TestApi(fx);
        Assert.Equal(HttpStatusCode.OK, (await api.As(s.Reviewer).GetAsync($"/api/review/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.As(s.Admin).GetAsync($"/api/reports/exams/{s.Exam.Id}/sessions/{session.Id}")).StatusCode);

        // Erişimi olmayan değerlendiricinin isteği veri göstermediği için görüntüleme kaydı da yok.
        Assert.Equal(HttpStatusCode.NotFound, (await api.As(s.OtherReviewer).GetAsync($"/api/review/sessions/{session.Id}")).StatusCode);

        await using var check = fx.CreateContext();
        var logs = await check.AuditLogs.AsNoTracking().Where(a => a.TargetId == session.Id).ToListAsync();

        var view = Assert.Single(logs, a => a.Action == "review.view");
        Assert.Equal(s.Reviewer.Id, view.ActorUserId);
        using (var details = JsonDocument.Parse(view.NewValue!))
        {
            Assert.Equal(1, details.RootElement.GetProperty("evidenceLinks").GetInt32());
        }

        Assert.Equal(s.Admin.Id, Assert.Single(logs, a => a.Action == "report.view").ActorUserId);
        Assert.DoesNotContain(logs, a => a.ActorUserId == s.OtherReviewer.Id);
    }
}
