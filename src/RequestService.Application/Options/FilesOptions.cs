namespace RequestService.Application.Options;

/// <summary>Connection settings for the files service.</summary>
public sealed class FilesOptions
{
    public const string SectionName = "Files";

    /// <summary>Base URL of the files service (e.g. https://storage.sabzevar.ir or http://127.0.0.1:6000).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Path that returns file metadata for a given id (GET).</summary>
    public string GetFilePath { get; set; } = "/api/files/{id}";

    /// <summary>
    /// Service-to-service JWT issued by the files service for request-service.
    /// TODO(coordination): the files team must issue a token signed with the files
    /// service Jwt (issuer storage.sabzevar.ir, its own Jwt:Key). Until provided,
    /// fileId validation against the real files service returns 401 and creation
    /// fails with a clear 503 dependency error — or disable via ValidationEnabled=false.
    /// </summary>
    public string? ServiceToken { get; set; }

    /// <summary>When false, fileId existence is not checked (development/demo only).</summary>
    public bool ValidationEnabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 10;
    public int RetryCount { get; set; } = 2;
}
