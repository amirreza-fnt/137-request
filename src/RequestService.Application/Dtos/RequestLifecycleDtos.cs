using RequestService.Domain.Enums;

namespace RequestService.Application.Dtos;

public sealed record UpdateRequestStatusRequest(string Status, string? Reason);

public sealed record ReferRequestRequest(string ToGroupId, string? Reason);

public sealed record RequestFileDto(string FileId, string FileType, DateTime CreatedAtUtc);

public sealed record RequestDetailResponse(
    Guid Id,
    string TrackingCode,
    string? NationalCode,
    string? Description,
    double? LocationLat,
    double? LocationLng,
    string Channel,
    string Status,
    string? CurrentGroupId,
    string? CreatedBySourcePhone,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<RequestFileDto> Files);

public sealed record UpdateRequestStatusResponse(Guid Id, string Status, string? CurrentGroupId);

public sealed record ReferRequestResponse(Guid Id, string Status, string CurrentGroupId);
