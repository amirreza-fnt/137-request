namespace RequestService.Domain.Enums;

/// <summary>Append-only event types recorded in <c>RequestLogs</c>.</summary>
public enum RequestActionType
{
    Created,
    ReviewedByOperator,
    Confirmed,
    Referred,
    GroupChanged,
    Rejected,
    StatusChanged,
    Updated,
    Closed,
    Canceled
}
