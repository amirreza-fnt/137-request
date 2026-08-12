using System.Globalization;
using RequestService.Application.Interfaces;

namespace RequestService.Application.Services;

/// <summary>
/// Builds tracking codes in the format <c>137-{yyyyMMdd Jalali}-{000001}</c>.
/// Uniqueness is guaranteed by the DB sequence consumed via
/// <see cref="IRequestRepository.GetNextSequenceValueAsync"/> plus the unique
/// index on <c>Requests.TrackingCode</c> — safe under concurrency.
/// </summary>
public sealed class TrackingCodeGenerator : ITrackingCodeGenerator
{
    private const string Prefix = "137-";
    private static readonly PersianCalendar Calendar = new();

    private readonly IRequestRepository _repository;

    public TrackingCodeGenerator(IRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> GenerateAsync(CancellationToken ct)
    {
        var seq = await _repository.GetNextSequenceValueAsync(ct);
        var now = DateTime.UtcNow;
        var datePart = $"{Calendar.GetYear(now):D4}{Calendar.GetMonth(now):D2}{Calendar.GetDayOfMonth(now):D2}";
        return $"{Prefix}{datePart}-{seq:D6}";
    }
}
