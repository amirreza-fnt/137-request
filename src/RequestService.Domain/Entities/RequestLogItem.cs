using RequestService.Domain.Enums;

namespace RequestService.Domain.Entities;

/// <summary>
/// Append-only audit/event record. Rows are never updated or deleted.
/// Free-form JSON may be stored in <see cref="Description"/> (e.g. temporary
/// name/phone metadata collected over the phone before the citizen is registered
/// in the SSO service — see SERVICE_CATALOG.md, Open Items).
/// </summary>
public class RequestLogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RequestId { get; set; }

    public RequestActionType ActionType { get; set; }

    public ActorType ActorType { get; set; }

    /// <summary>ID of the actor (operator personnel code / citizen national code / null for System).</summary>
    public string? ActorId { get; set; }

    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }

    public string? PreviousGroupId { get; set; }
    public string? NewGroupId { get; set; }

    /// <summary>Free-form details (optionally JSON metadata).</summary>
    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Request? Request { get; set; }
}
