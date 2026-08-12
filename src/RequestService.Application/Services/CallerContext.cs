namespace RequestService.Application.Services;

/// <summary>Who is making the call, resolved from the API key and/or the SSO bearer token.</summary>
public enum CallerKind
{
    /// <summary>No usable identity — allowed only for internal-service callers with a valid API key.</summary>
    None,
    Citizen,
    Operator,
    /// <summary>Internal service (e.g. the telephony IVR) authenticated with an API key.</summary>
    ExternalService
}

public sealed record CallerContext(
    CallerKind Kind,
    string? ActorId,
    string? MelliCode,
    string? Phone);
