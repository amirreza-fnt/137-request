using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Infrastructure.Clients;

/// <summary>
/// Calls referral-service POST /api/v1/referrals/{id}/bootstrap after a request is created.
/// Auth: X-Api-Key (InternalAuth of referral-service).
/// </summary>
public sealed class ReferralBootstrapClient : IReferralBootstrapClient
{
    private readonly HttpClient _http;
    private readonly IOptions<ReferralBootstrapOptions> _options;
    private readonly ILogger<ReferralBootstrapClient> _logger;

    public ReferralBootstrapClient(
        HttpClient http,
        IOptions<ReferralBootstrapOptions> options,
        ILogger<ReferralBootstrapClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task TryBootstrapAsync(
        Guid requestId,
        string? description,
        double? lat,
        double? lng,
        CancellationToken ct)
    {
        if (!_options.Value.Enabled || string.IsNullOrWhiteSpace(_options.Value.BaseUrl))
        {
            return;
        }

        var path = _options.Value.BootstrapPath.Replace("{id}", requestId.ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = JsonContent.Create(new
        {
            description,
            lat,
            lng,
            initialGroupId = (string?)null
        });

        if (!string.IsNullOrWhiteSpace(_options.Value.ServiceToken))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _options.Value.ServiceToken);
        }

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Referral bootstrap for {RequestId} returned HTTP {Status}: {Body}",
                    requestId, (int)response.StatusCode, body);
                return;
            }

            _logger.LogInformation("Referral workflow bootstrapped for request {RequestId}.", requestId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex,
                "Referral bootstrap failed for request {RequestId} (request remains created; retry via POST /bootstrap).",
                requestId);
        }
    }
}
