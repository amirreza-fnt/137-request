namespace RequestService.Application.Interfaces;

/// <summary>Starts the first workflow step in referral-service for a newly created request.</summary>
public interface IReferralBootstrapClient
{
    /// <summary>
    /// Best-effort bootstrap. Failures are logged and must not roll back request creation.
    /// </summary>
    Task TryBootstrapAsync(
        Guid requestId,
        string? description,
        double? lat,
        double? lng,
        CancellationToken ct);
}
