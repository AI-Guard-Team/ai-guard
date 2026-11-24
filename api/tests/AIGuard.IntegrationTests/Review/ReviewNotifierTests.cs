using AIGuard.Api.Review;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Enums;
using AIGuard.IntegrationTests.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.IntegrationTests.Review;

/// <summary>A4: bildirim gönderilemese de teslim geçerli kalmalı.</summary>
public class ReviewNotifierTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SignalR_failure_is_logged_not_thrown()
    {
        var notifier = new SignalRReviewNotifier(new BrokenHubContext(), NullLogger<SignalRReviewNotifier>.Instance);

        // Hata yukarı çıkmıyor; teslim yolunu bozmuyor.
        await notifier.SessionReadyForReviewAsync(new SessionReadyForReview(Guid.NewGuid(), Guid.NewGuid(), 12, [SessionFlags.Risky], null));
    }

    [SkippableFact]
    public async Task Submit_succeeds_even_when_the_notification_cannot_be_sent()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        await using var db = fx.CreateContext();
        var clock = new FakeTimeProvider(EventTestHelpers.Now.AddMinutes(5));
        var session = await EventTestHelpers.InProgressSessionAsync(db);
        session.Flags = SessionFlags.Risky;
        await db.SaveChangesAsync();
        var notifier = new SignalRReviewNotifier(new BrokenHubContext(), NullLogger<SignalRReviewNotifier>.Instance);

        var result = await new SessionService(db, EventTestHelpers.Service(db, clock), clock, notifier).SubmitAsync(session.Id);

        Assert.Equal(SessionOperationStatus.Success, result.Status);
        db.ChangeTracker.Clear();
        Assert.Equal(SessionState.Completed, (await db.ExamSessions.SingleAsync(s => s.Id == session.Id)).State);
    }

    /// <summary>Her gönderimde hata veren hub (ör. bağlantı altyapısı çöktü).</summary>
    private sealed class BrokenHubContext : IHubContext<ReviewHub>, IHubClients, IClientProxy
    {
        IHubClients IHubContext<ReviewHub>.Clients => this;

        IGroupManager IHubContext<ReviewHub>.Groups => throw new NotSupportedException();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Hub erişilemiyor.");

        public IClientProxy All => this;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => this;

        public IClientProxy Client(string connectionId) => this;

        IClientProxy IHubClients<IClientProxy>.Clients(IReadOnlyList<string> connectionIds) => this;

        public IClientProxy Group(string groupName) => this;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => this;

        IClientProxy IHubClients<IClientProxy>.Groups(IReadOnlyList<string> groupNames) => this;

        public IClientProxy User(string userId) => this;

        public IClientProxy Users(IReadOnlyList<string> userIds) => this;
    }
}
