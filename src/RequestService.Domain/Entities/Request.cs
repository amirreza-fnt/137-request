using RequestService.Domain.Enums;

namespace RequestService.Domain.Entities;

/// <summary>
/// Current snapshot of a citizen request. Only the *current* state lives here;
/// every mutation is appended to <see cref="RequestLogItem"/> (append-only).
/// Per the architecture decision, no redundant PII is stored — only NationalCode.
/// </summary>
public class Request
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>5-digit tracking code from the centralized coding service (e.g. 00042).</summary>
    public string TrackingCode { get; set; } = string.Empty;

    /// <summary>National code of the citizen. Nullable (telephony scenario without verified identity).</summary>
    public string? NationalCode { get; set; }

    public string? Description { get; set; }

    public decimal? LocationLat { get; set; }
    public decimal? LocationLng { get; set; }

    public RequestChannel Channel { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.New;

    /// <summary>ID of the department/group currently responsible (Nullable until referral).</summary>
    public string? CurrentGroupId { get; set; }

    /// <summary>Source phone number in the telephony (call-center) scenario.</summary>
    public string? CreatedBySourcePhone { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<RequestFile> Files { get; set; } = new List<RequestFile>();
    public ICollection<RequestLogItem> Logs { get; set; } = new List<RequestLogItem>();
}
