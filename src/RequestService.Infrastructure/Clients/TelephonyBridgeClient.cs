using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Infrastructure.Clients;

public sealed class TelephonyBridgeClient : ITelephonyBridgeClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly TelephonyOptions _options;

    public TelephonyBridgeClient(HttpClient http, IOptions<TelephonyOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<object> GetActiveCallsAsync(string? queue, CancellationToken ct)
    {
        EnsureConfigured();
        var q = string.IsNullOrWhiteSpace(queue) ? _options.DefaultQueue : queue.Trim();
        var url = $"api/active-calls.php?queue={Uri.EscapeDataString(q)}&secret={Uri.EscapeDataString(_options.BridgeSecret)}";
        try
        {
            using var res = await _http.GetAsync(url, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return ParseOrWrap(res, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new
            {
                ok = false,
                configured = true,
                message = "Cannot reach IssabelBridge: " + ex.Message,
                callers = Array.Empty<object>(),
                members = Array.Empty<object>(),
                peers = Array.Empty<object>(),
                membersOnline = 0,
                waiting = 0
            };
        }
    }

    public async Task<object> ControlAsync(
        string action,
        string channel,
        string? exten,
        string? context,
        CancellationToken ct)
    {
        EnsureConfigured();
        var payload = new Dictionary<string, string?>
        {
            ["secret"] = _options.BridgeSecret,
            ["action"] = action,
            ["channel"] = channel,
            ["exten"] = string.IsNullOrWhiteSpace(exten) ? _options.DefaultAgentExten : exten,
            ["context"] = string.IsNullOrWhiteSpace(context) ? _options.DefaultContext : context
        };

        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/call-control.php") { Content = content };
            req.Headers.TryAddWithoutValidation("X-Bridge-Secret", _options.BridgeSecret);
            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return ParseOrWrap(res, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new
            {
                ok = false,
                message = "Cannot reach IssabelBridge: " + ex.Message
            };
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.BridgeBaseUrl) || string.IsNullOrWhiteSpace(_options.BridgeSecret))
        {
            throw new InvalidOperationException(
                "Telephony bridge is not configured. Set Telephony:BridgeBaseUrl and Telephony:BridgeSecret.");
        }
    }

    private static object ParseOrWrap(HttpResponseMessage res, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            return doc.RootElement.Clone();
        }
        catch
        {
            return new
            {
                ok = res.IsSuccessStatusCode,
                status = (int)res.StatusCode,
                body
            };
        }
    }
}
