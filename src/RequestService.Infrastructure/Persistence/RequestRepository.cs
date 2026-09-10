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

    public async Task<bool> RequestFileExistsAsync(string fileId, CancellationToken ct)
        => await _db.RequestFiles.AsNoTracking()
            .AnyAsync(f => f.FileId == fileId, ct);

    public async Task<Request?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Requests
            .Include(r => r.Files)
            .Include(r => r.Logs)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Request?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct)
        => await _db.Requests
            .Include(r => r.Files)
            .Include(r => r.Logs)
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

        if (digits.Length <= 5)
        {
            var padded = digits.PadLeft(5, '0');
            exact = await GetByTrackingCodeAsync(padded, ct);
            if (exact is not null)
            {
                return exact;
            }
        }

        return await _db.Requests
            .Include(r => r.Files)
            .Include(r => r.Logs)
            .FirstOrDefaultAsync(r => r.TrackingCode.Replace("-", "") == digits, ct);
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
        IQueryable<Request> query = _db.Requests.AsNoTracking()
            .Include(r => r.Files)
            .Include(r => r.Logs);

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
