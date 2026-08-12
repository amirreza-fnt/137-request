namespace RequestService.Domain.Enums;

/// <summary>Current lifecycle status of a request. Stored as a string in the DB (per sibling convention).</summary>
public enum RequestStatus
{
    New,
    UnderReview,
    InProgress,
    Referred,
    Rejected,
    Closed,
    Canceled
}
