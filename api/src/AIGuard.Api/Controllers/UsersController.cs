using AIGuard.Api.Auth;
using AIGuard.Application.Users;
using AIGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.Api.Controllers;

public sealed record CreatedUserResponse(Guid Id);

/// <summary>
/// Yöneticinin kullanıcı yönetimi (E2, FR-01, FR-03). POST kartta var; GET ve PATCH FR-03 ve
/// E6'daki yönetici ekranı için eklendi (K-20).
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AccessPolicies.Manage)]
public sealed class UsersController(UserService users) : ControllerBase
{
    /// <summary>Kullanıcıyı oluşturur ve e-postasına tek kullanımlık davet bağlantısı gönderir.</summary>
    [HttpPost]
    [RequestSizeLimit(4096)]
    [ProducesResponseType<CreatedUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await users.CreateAsync(User.GetUserId(), request, ct);
        return result.Status switch
        {
            UserOperationStatus.Success => StatusCode(StatusCodes.Status201Created, new CreatedUserResponse(result.Id!.Value)),
            UserOperationStatus.EmailTaken => Problem(statusCode: StatusCodes.Status409Conflict, title: "E-posta kullanımda.", detail: result.Error),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error),
        };
    }

    /// <param name="role">İsteğe bağlı rol süzgeci (ör. atama ekranında yalnızca adaylar).</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserSummary>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UserSummary>> List([FromQuery] UserRole? role, CancellationToken ct) =>
        await users.ListAsync(role, ct);

    /// <summary>Rol atama ve pasife alma/etkinleştirme; yalnızca gönderilen alanlar değişir.</summary>
    [HttpPatch("{id:guid}")]
    [RequestSizeLimit(1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var result = await users.UpdateAsync(User.GetUserId(), id, request, ct);
        return result.Status switch
        {
            UserOperationStatus.Success => NoContent(),
            UserOperationStatus.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek.", detail: result.Error),
        };
    }
}
