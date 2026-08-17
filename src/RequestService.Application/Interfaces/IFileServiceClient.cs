namespace RequestService.Application.Interfaces;

/// <summary>Metadata of a file stored in the files service.</summary>
public sealed record FileMetadataDto(Guid Id, string? Extension, string? MimeType, string? ShortCode, string? AccessType);

public sealed record FileStreamResult(Stream Content, string? ContentType, string? FileName);

/// <summary>
/// Validates that a FileId really exists in the files service. The file bytes
/// are never downloaded here — only metadata is checked.
/// </summary>
public interface IFileServiceClient
{
    /// <summary>
    /// Returns file metadata, or null when the file does not exist.
    /// Throws <see cref="Exceptions.DependencyUnavailableException"/> when the
    /// files service is unreachable or rejects the service token.
    /// </summary>
    Task<FileMetadataDto?> GetFileAsync(string fileId, CancellationToken ct);

    /// <summary>Streams file bytes via files-service short link (admin JWT).</summary>
    Task<FileStreamResult?> StreamFileAsync(string fileId, CancellationToken ct);
}
