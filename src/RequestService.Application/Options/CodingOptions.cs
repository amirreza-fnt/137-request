namespace RequestService.Application.Options;

/// <summary>Centralized tracking-code service (Request Coding Service).</summary>
public sealed class CodingOptions
{
    public const string SectionName = "Coding";

    public string BaseUrl { get; set; } = string.Empty;

    public int SystemId { get; set; } = 1;

    public string CreatePath { get; set; } = "/api/v1/tracking-codes";

    /// <summary>X-Api-Key sent to the coding service (defaults to InternalAuth key if empty).</summary>
    public string? ServiceToken { get; set; }

    public int TimeoutSeconds { get; set; } = 10;

    public int RetryCount { get; set; } = 2;

    public int CircuitBreakerMinThroughput { get; set; } = 8;

    public int CircuitBreakerFailureRatio { get; set; } = 50;
}
