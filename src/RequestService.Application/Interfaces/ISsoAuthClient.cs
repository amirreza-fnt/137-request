namespace RequestService.Application.Interfaces;

/// <summary>Identity of a citizen as returned by sso-login-service after token validation.</summary>
public sealed record SsoUserInfo(string UserId, string MelliCode, string? Phone);

/// <summary>
/// Validates a citizen/operator JWT against sso-login-service and extracts the
/// national code. Independent layer so the SSO service can evolve without
/// touching the rest of the request-service.
/// </summary>
public interface ISsoAuthClient
{
    /// <summary>
    /// Validates <paramref name="token"/> against sso-login-service.
    /// Returns null when the token is invalid/expired; throws
    /// <see cref="Exceptions.DependencyUnavailableException"/> when SSO is unreachable.
    /// </summary>
    Task<SsoUserInfo?> ValidateAsync(string token, CancellationToken ct);
}
