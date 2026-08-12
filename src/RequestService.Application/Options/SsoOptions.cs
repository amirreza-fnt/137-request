namespace RequestService.Application.Options;

/// <summary>Connection settings for the sso-login-service.</summary>
public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    /// <summary>Base URL of sso-login-service (e.g. https://apiweb-loginsso.sabzevar.ir or http://127.0.0.1:5001).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>User-info endpoint path (validated token → citizen identity).</summary>
    public string UserInfoPath { get; set; } = "/api/auth/me";

    public int TimeoutSeconds { get; set; } = 10;
    public int RetryCount { get; set; } = 2;
    public int CircuitBreakerMinThroughput { get; set; } = 8;
    public int CircuitBreakerFailureRatio { get; set; } = 50; // percent
}
