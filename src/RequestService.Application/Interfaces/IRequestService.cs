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
}
