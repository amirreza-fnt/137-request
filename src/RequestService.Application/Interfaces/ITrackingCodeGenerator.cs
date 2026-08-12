namespace RequestService.Application.Interfaces;

/// <summary>Generates a unique, human-readable tracking code (e.g. 137-20260812-000482).</summary>
public interface ITrackingCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken ct);
}
