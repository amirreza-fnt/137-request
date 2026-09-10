namespace RequestService.Application.Interfaces;

public sealed record CodingTrackingCodeResult(
    Guid CodingRequestId,
    string TrackingCode,
    int Counter);

public interface ICodingServiceClient
{
    Task<CodingTrackingCodeResult> AllocateTrackingCodeAsync(
        string nationalCode,
        string firstName,
        string lastName,
        string? mobile,
        string? landline,
        string? description,
        string apiKey,
        CancellationToken ct);
}
