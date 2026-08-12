namespace RequestService.Application.Exceptions;

/// <summary>Input/domain-rule violation → HTTP 400.</summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}

/// <summary>Missing/invalid caller identity → HTTP 401.</summary>
public sealed class NotAuthenticatedException : Exception
{
    public NotAuthenticatedException(string message) : base(message) { }
}

/// <summary>The caller is authenticated but not permitted for this operation → HTTP 403.</summary>
public sealed class NotAuthorizedException : Exception
{
    public NotAuthorizedException(string message) : base(message) { }
}

/// <summary>A required downstream dependency (SSO/files/DB) is unavailable → HTTP 503.</summary>
public sealed class DependencyUnavailableException : Exception
{
    public DependencyUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Requested resource does not exist → HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
