using AIGuard.Api.Auth;
using AIGuard.Application.Events;
using AIGuard.Application.Exams;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

public sealed record StartSessionResponse(Guid SessionId, SessionState State);

/// <summary>
/// Sınav oturumu (E4, FR-09): başlatma, oturum görünümü, cevap kaydı ve teslim. Yalnızca aday ve yalnızca
/// kendi oturumu (K-20). Durum geçişleri SessionStateMachine'den; tablo dışı geçiş 409 (K-24).
/// </summary>
[ApiController]
[Authorize(Policy = AccessPolicies.TakeExam)]
public sealed class SessionsController(SessionService sessions, SessionAccessService access, SessionIpGuard ipGuard) : ControllerBase
{
    /// <summary>
    /// Sınavı başlatır (ConsentPending). Adayın açık bir oturumu varsa aynısı döner (200); yeni oturum 201.
    /// Pencere dışında ya da hak kullanılmışsa 409.
    /// </summary>
    [HttpPost("api/exams/{examId:guid}/sessions")]
    [ProducesResponseType<StartSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<StartSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(Guid examId, CancellationToken ct)
    {
        var result = await sessions.StartAsync(examId, User.GetUserId(), ct);
        if (result.SessionId is { } id && result.Status is StartSessionStatus.Created or StartSessionStatus.Resumed)
        {
            // İlk IP oturuma yazılıyor; sonraki isteklerde değişirse IP_CHANGE (FR-22, K-16).
            await ipGuard.CheckAsync(id, HttpContext.Connection.RemoteIpAddress, ct);
        }

        return result.Status switch
        {
            StartSessionStatus.Created => StatusCode(StatusCodes.Status201Created, new StartSessionResponse(result.SessionId!.Value, result.State!.Value)),
            StartSessionStatus.Resumed => Ok(new StartSessionResponse(result.SessionId!.Value, result.State!.Value)),
            StartSessionStatus.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Sınav başlatılamıyor.", detail: Message(result.Denial)),
        };
    }

    /// <summary>
    /// Adayın oturumu: durum, sunucuya göre bitiş zamanı, sorular (doğru seçeneksiz, yalnızca sınav sürerken)
    /// ve kayıtlı cevaplar. Sınav sırasında en geç 10 sn'de bir çağrılmalı; bağlantı kesintisi buna göre anlaşılıyor.
    /// </summary>
    [HttpGet("api/sessions/{id:guid}")]
    [ProducesResponseType<SessionView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!await access.IsCandidateOfAsync(id, User.GetUserId(), ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(id, HttpContext.Connection.RemoteIpAddress, ct);
        Response.Headers.CacheControl = "no-store"; // kalan süre ve cevaplar önbellekten gelmesin
        return await sessions.GetAsync(id, ct) is { } view ? Ok(view) : NotFound();
    }

    /// <summary>
    /// Cevap kaydı: <c>{ questionId, selectedOption, seq, clientTime }</c>. Bir soru için en yüksek seq geçerli;
    /// aynı seq tekrar gelirse 200 (kayıt yok). Süre dolduktan sonra 409.
    /// </summary>
    [HttpPost("api/sessions/{id:guid}/answers")]
    [RequestSizeLimit(1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Answer(Guid id, AnswerRequest request, CancellationToken ct)
    {
        if (!await access.IsCandidateOfAsync(id, User.GetUserId(), ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(id, HttpContext.Connection.RemoteIpAddress, ct);
        return ToResponse(await sessions.SaveAnswerAsync(id, request, ct), created: true);
    }

    /// <summary>Sınavı teslim eder (Completed). Zaten teslim edildiyse ya da sınav başlamadıysa 409.</summary>
    [HttpPost("api/sessions/{id:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        if (!await access.IsCandidateOfAsync(id, User.GetUserId(), ct))
        {
            return NotFound();
        }

        await ipGuard.CheckAsync(id, HttpContext.Connection.RemoteIpAddress, ct);
        return ToResponse(await sessions.SubmitAsync(id, ct));
    }

    private IActionResult ToResponse(SessionOperationResult result, bool created = false) => result.Status switch
    {
        SessionOperationStatus.Success when created => StatusCode(StatusCodes.Status201Created),
        SessionOperationStatus.Success => NoContent(),
        SessionOperationStatus.Duplicate => Ok(),
        SessionOperationStatus.NotFound => NotFound(),
        SessionOperationStatus.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "İşlem yapılamıyor.", detail: result.Error),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error),
    };

    private static string Message(StartDenial? denial) => denial switch
    {
        StartDenial.WindowNotOpen => "Sınavın erişim penceresi henüz açılmadı.",
        StartDenial.TooLate => "Sınava son başlangıç zamanı geçti.",
        StartDenial.AttemptUsed => "Bu sınav için oturum hakkınızı kullandınız.",
        _ => "Bu sınava atanmadınız.",
    };
}
