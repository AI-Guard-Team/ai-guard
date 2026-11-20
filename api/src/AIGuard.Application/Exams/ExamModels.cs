using AIGuard.Domain.Enums;

namespace AIGuard.Application.Exams;

// İstek alanları null olabilir: eksik alan model doğrulamasında değil serviste Türkçe mesajla reddediliyor.

public sealed record ExamRequest(
    string? Name,
    ExamType? Type,
    int? DurationMinutes,
    DateTimeOffset? WindowStartAt,
    DateTimeOffset? WindowEndAt,
    int? RetentionDays);

/// <summary>
/// Sınav listesi ve ayrıntısı. Sorular ve doğru seçenek burada hiç yok; aday tarafına giden
/// yanıtlarda doğru seçenek asla dönmemeli (kart E3).
/// </summary>
/// <param name="LatestStartAt">Son başlangıç zamanı = pencere bitişi − süre (FR-06).</param>
/// <param name="AttemptUsed">Yalnızca adaya dönüyor: tek oturum hakkı kullanıldı mı (GAB 3.3.1). Diğer rollerde null.</param>
public sealed record ExamSummary(
    Guid Id,
    string Name,
    ExamType Type,
    int DurationMinutes,
    DateTimeOffset WindowStartAt,
    DateTimeOffset WindowEndAt,
    DateTimeOffset LatestStartAt,
    int RetentionDays,
    int QuestionCount,
    bool? AttemptUsed);

public sealed record QuestionRequest(string? Text, IReadOnlyList<string>? Options, int? CorrectOptionIndex, int? Order);

/// <summary>Yalnızca yöneticiye dönüyor (doğru seçenek dahil). Adaya soru E4'teki oturum ucundan, doğru seçeneksiz gidecek.</summary>
public sealed record QuestionDetail(Guid Id, string Text, IReadOnlyList<string> Options, int CorrectOptionIndex, int Order);

public sealed record AssignmentRequest(Guid? UserId, AssignmentRole? Role);

public sealed record AssignmentDetail(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    AssignmentRole Role,
    DateTimeOffset AssignedAt,
    DateTimeOffset? AttemptUsedAt);

public enum ExamOperationStatus
{
    Success,
    NotFound,
    Invalid,

    /// <summary>Sınavda oturum başlamış; içerik artık değiştirilemiyor ya da kayıt zaten var.</summary>
    Conflict,
}

public sealed record ExamOperationResult(ExamOperationStatus Status, string? Error = null, Guid? Id = null)
{
    public static ExamOperationResult Ok(Guid? id = null) => new(ExamOperationStatus.Success, Id: id);

    public static readonly ExamOperationResult NotFound = new(ExamOperationStatus.NotFound);

    public static ExamOperationResult Invalid(string error) => new(ExamOperationStatus.Invalid, error);

    public static ExamOperationResult Conflict(string error) => new(ExamOperationStatus.Conflict, error);
}
