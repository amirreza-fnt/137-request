using Microsoft.EntityFrameworkCore;
using RequestService.Application.Exceptions;
using RequestService.Application.Interfaces;
using RequestService.Domain.Entities;
using RequestService.Domain.Enums;

namespace RequestService.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRequestRepository"/>. Every method that
/// mutates more than one aggregate (Request + RequestLogs ± RequestFiles) runs
/// inside one database transaction.
/// </summary>
public sealed class RequestRepository : IRequestRepository
{
    private readonly RequestDbContext _db;

    public RequestRepository(RequestDbContext db)
    {
        _db = db;
    }

    public async Task<long> GetNextSequenceValueAsync(CancellationToken ct)
    {
        // Sequence created by the initial migration; monotonic → race-free.
        // (NEXT VALUE FOR must not be wrapped in a subquery, so use ADO.NET directly.)
        var connection = _db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;

        if (opened)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT NEXT VALUE FOR dbo.RequestTrackingCodeSeq";
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> TrackingCodeExistsAsync(string trackingCode, CancellationToken ct)
        => await _db.Requests.AsNoTracking()
            .AnyAsync(r => r.TrackingCode == trackingCode, ct);

    public async Task<Request?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Requests
            .Include(r => r.Files)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Request?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct)
        => await _db.Requests
            .Include(r => r.Files)
            .FirstOrDefaultAsync(r => r.TrackingCode == trackingCode, ct);

    public async Task<Request?> FindByTrackingCodeFlexibleAsync(string codeOrDigits, CancellationToken ct)
    {
        var raw = (codeOrDigits ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        var exact = await GetByTrackingCodeAsync(raw, ct);
        if (exact is not null)
        {
            return exact;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        // Reconstruct canonical form: 137 + yyyyMMdd(8) + seq(6) → 137-yyyyMMdd-seq
        if (digits.Length == 17 && digits.StartsWith("137", StringComparison.Ordinal))
        {
            var reconstructed = $"137-{digits.Substring(3, 8)}-{digits.Substring(11, 6)}";
            var byReconstructed = await GetByTrackingCodeAsync(reconstructed, ct);
            if (byReconstructed is not null)
            {
                return byReconstructed;
            }
        }

        // Compare digit-only form in SQL (for partial / TTS digit strings)
        return await _db.Requests
            .Include(r => r.Files)
            .AsNoTracking()
            .Where(r => r.TrackingCode.Replace("-", "") == digits
                        || r.TrackingCode.Replace("-", "").EndsWith(digits))
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task CreateAsync(
        Request request,
        IReadOnlyCollection<RequestFile> files,
        RequestLogItem createdLog,
        CancellationToken ct)
    {
        await RunInTransactionAsync(async () =>
        {
            _db.Requests.Add(request);

            if (files.Count > 0)
            {
                _db.RequestFiles.AddRange(files);
            }

            _db.RequestLogs.Add(createdLog);
            await _db.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task UpdateStatusAsync(Request request, RequestLogItem log, CancellationToken ct)
    {
        await RunInTransactionAsync(async () =>
        {
            _db.Requests.Update(request);
            _db.RequestLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task ReferAsync(Request request, string toGroupId, RequestLogItem log, CancellationToken ct)
    {
        await RunInTransactionAsync(async () =>
        {
            request.CurrentGroupId = toGroupId;
            request.Status = RequestStatus.Referred;
            request.UpdatedAtUtc = DateTime.UtcNow;
            _db.Requests.Update(request);
            _db.RequestLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Runs the work inside a single database transaction. The EF retry strategy
    /// (SqlServerRetryingExecutionStrategy) requires user-initiated transactions
    /// to run through CreateExecutionStrategy so a transient failure retries the
    /// whole unit (not just the last statement). The change tracker is cleared on
    /// every attempt to keep inserts idempotent across retries.
    /// </summary>
    private async Task RunInTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await work();
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<Request>> SearchAsync(
        RequestStatus? status,
        string? currentGroupId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct)
    {
        var query = _db.Requests.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(currentGroupId))
        {
            query = query.Where(r => r.CurrentGroupId == currentGroupId);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(r => r.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(r => r.CreatedAtUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(200)
            .ToListAsync(ct);
    }
}
