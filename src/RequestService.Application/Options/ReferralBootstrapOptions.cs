namespace RequestService.Application.Options;

/// <summary>
/// Fire-and-forget bootstrap of the first referral workflow step after a request is created.
/// </summary>
public sealed class ReferralBootstrapOptions
{
    public const string SectionName = "ReferralBootstrap";

    /// <summary>Base URL of referral-service (e.g. http://127.0.0.1:5070).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>POST path template. {id} is replaced with the request Guid.</summary>
    public string BootstrapPath { get; set; } = "/api/v1/referrals/{id}/bootstrap";

    /// <summary>X-Api-Key value accepted by referral InternalAuth.</summary>
    public string? ServiceToken { get; set; }

    /// <summary>When false, create does not call referral-service (dev/offline).</summary>
    public bool Enabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 10;
    public int RetryCount { get; set; } = 2;
}
