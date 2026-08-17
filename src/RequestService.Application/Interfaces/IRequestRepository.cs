using RequestService.Domain.Entities;
using RequestService.Domain.Enums;

namespace RequestService.Application.Interfaces;

/// <summary>
/// Persistence contract. All operations that touch <c>Request</c> together with
/// <c>RequestLogs</c> (and <c>RequestFiles</c>) are executed atomically inside a
/// single database transaction (see implementations).
/// </summary>
public interface IRequestRepository
{
    /// <summary>Next value of the DB sequence used to build tracking codes.</summary>
    Task<long> GetNextSequenceValueAsync(CancellationToken ct);

    Task<bool> TrackingCodeExistsAsync(string trackingCode, CancellationToken ct);

    Task<bool> RequestFileExistsAsync(string fileId, CancellationToken ct);

    Task<Request?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Request?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct);

    /// <summary>
    /// Full tracking code or digit-only form (AGI <c>say_digits</c> strips dashes),
    /// e.g. <c>13714050526-000010</c> ↔ <c>137-14050526-000010</c>.
    /// </summary>
    Task<Request?> FindByTrackingCodeFlexibleAsync(string codeOrDigits, CancellationToken ct);

    /// <summary>Atomic insert of the request + files + Created log event.</summary>
    Task CreateAsync(Request request, IReadOnlyCollection<RequestFile> files, RequestLogItem createdLog, CancellationToken ct);

    /// <summary>Atomic status change: updates the request row and appends a log event.</summary>
    Task UpdateStatusAsync(Request request, RequestLogItem log, CancellationToken ct);

    /// <summary>Atomic referral: updates current group and appends a log event.</summary>
    Task ReferAsync(Request request, string toGroupId, RequestLogItem log, CancellationToken ct);

    Task<IReadOnlyList<Request>> SearchAsync(
        RequestStatus? status,
        string? currentGroupId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct);
}
