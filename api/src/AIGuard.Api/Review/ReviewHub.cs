using AIGuard.Api.Auth;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Api.Review;

/// <summary>
/// Değerlendirici bildirimleri (A4): oturum riskli veya işaretli olarak tamamlanınca "SessionReadyForReview".
/// İstemciden çağrılan bir yöntem yok; hub yalnızca sunucudan mesaj iletiyor.
/// </summary>
/// <remarks>
/// Her değerlendirici bağlanınca atandığı sınavların gruplarına ekleniyor; bildirim yalnızca o sınavın
/// grubuna (ve Admin'lere) gidiyor, başka sınavın oturumu duyulmuyor. Bağlantı sırasında yapılan yeni
/// atama bir sonraki bağlantıda geçerli olur; panel sayfa açılışında yeniden bağlanıyor.
/// Tarayıcı WebSocket'te başlık gönderemediği için belirteç <c>access_token</c> sorgu parametresinden
/// okunuyor (yalnızca /hubs yolunda, AuthenticationSetup).
/// </remarks>
[Authorize(Roles = $"{nameof(UserRole.Reviewer)},{nameof(UserRole.Admin)}")]
public sealed class ReviewHub(IAppDbContext db) : Hub
{
    public const string Path = "/hubs/review";
    public const string ReadyMessage = "SessionReadyForReview";
    public const string AdminsGroup = "admins";

    public static string ExamGroup(Guid examId) => $"exam:{examId:N}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        if (user.GetUserRole() == UserRole.Admin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        }
        else
        {
            var userId = user.GetUserId();
            var examIds = await db.ExamAssignments.AsNoTracking()
                .Where(a => a.UserId == userId && a.Role == AssignmentRole.Reviewer)
                .Select(a => a.ExamId)
                .ToListAsync(Context.ConnectionAborted);
            foreach (var examId in examIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ExamGroup(examId));
            }
        }

        await base.OnConnectedAsync();
    }
}

/// <summary><see cref="IReviewNotifier"/>'ın SignalR uygulaması.</summary>
public sealed class SignalRReviewNotifier(IHubContext<ReviewHub> hub, ILogger<SignalRReviewNotifier> logger) : IReviewNotifier
{
    public async Task SessionReadyForReviewAsync(SessionReadyForReview message, CancellationToken ct = default)
    {
        try
        {
            await hub.Clients.Groups(ReviewHub.ExamGroup(message.ExamId), ReviewHub.AdminsGroup)
                .SendAsync(ReviewHub.ReadyMessage, message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Teslim zaten kaydedildi; bildirim gitmese de oturum panel listesinde görünüyor.
            logger.LogWarning(ex, "İnceleme bildirimi gönderilemedi (oturum {SessionId}).", message.SessionId);
        }
    }
}
