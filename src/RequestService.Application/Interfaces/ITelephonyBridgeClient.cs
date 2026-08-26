namespace RequestService.Application.Interfaces;

public interface ITelephonyBridgeClient
{
    Task<object> GetActiveCallsAsync(string? queue, CancellationToken ct);

    Task<object> ControlAsync(
        string action,
        string channel,
        string? exten,
        string? context,
        CancellationToken ct);
}
