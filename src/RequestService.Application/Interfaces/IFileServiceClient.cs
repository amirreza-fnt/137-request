namespace RequestService.Application.Interfaces;

/// <summary>Metadata of a file stored in the files service.</summary>
public sealed record FileMetadataDto(Guid Id, string? Extension, string? MimeType);

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
}
