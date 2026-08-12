using RequestService.Domain.Enums;

namespace RequestService.Domain.Entities;

/// <summary>
/// Link between a request and an attached file. Only the FileId (issued by the
/// files service) is persisted — the actual file bytes are never stored here.
/// </summary>
public class RequestFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RequestId { get; set; }

    /// <summary>ID of the file in the files service (GUID as a string).</summary>
    public string FileId { get; set; } = string.Empty;

    public RequestFileType FileType { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Request? Request { get; set; }
}
