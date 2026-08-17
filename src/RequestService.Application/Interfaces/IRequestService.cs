using RequestService.Application.Dtos;

namespace RequestService.Application.Interfaces;

public interface IRequestService
{
    /// <summary>
    /// Creates a new citizen request. Enforces channel-specific validation and
    /// authentication, validates attached file ids against the files service, and
    /// persists the request + files + Created audit log in one transaction.
    /// </summary>
    Task<CreateRequestResponse> CreateAsync(
        CreateRequestRequest request,
        string? authorizationHeader,
        string? apiKeyHeader,
        CancellationToken ct);

    Task<RequestDetailResponse> GetByIdAsync(Guid id, string? authorizationHeader, string? apiKeyHeader, CancellationToken ct);

    /// <summary>Citizen / IVR lookup by tracking code (full or digit-only from TTS).</summary>
    Task<RequestDetailResponse> GetByTrackingCodeAsync(string code, CancellationToken ct);

    Task<UpdateRequestStatusResponse> UpdateStatusAsync(
        Guid id,
        UpdateRequestStatusRequest request,
        string? authorizationHeader,
        string? apiKeyHeader,
        CancellationToken ct);

    Task<ReferRequestResponse> ReferAsync(
        Guid id,
        ReferRequestRequest request,
        string? authorizationHeader,
        string? apiKeyHeader,
        CancellationToken ct);
}
