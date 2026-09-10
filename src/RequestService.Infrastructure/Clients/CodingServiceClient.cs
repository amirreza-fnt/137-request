using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RequestService.Application.Exceptions;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Infrastructure.Clients;

public sealed class CodingServiceClient : ICodingServiceClient
{
    private readonly HttpClient _http;
    private readonly CodingOptions _options;
    private readonly ILogger<CodingServiceClient> _logger;

    public CodingServiceClient(HttpClient http, IOptions<CodingOptions> options, ILogger<CodingServiceClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CodingTrackingCodeResult> AllocateTrackingCodeAsync(
        string nationalCode,
        string firstName,
        string lastName,
        string? mobile,
        string? landline,
        string? description,
        string apiKey,
        CancellationToken ct)
    {
        var payload = new
        {
            systemId = _options.SystemId,
            nationalCode,
            firstName,
            lastName,
            mobile,
            landline,
            description
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.CreatePath.TrimStart('/'));
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await _http.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new NotAuthenticatedException("Coding service rejected the API key.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Coding service returned {Status}: {Body}",
                    (int)response.StatusCode,
                    body);
                throw new DependencyUnavailableException(
                    $"Coding service returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<CodingCreateResponse>(cancellationToken: ct)
                ?? throw new DependencyUnavailableException("Coding service returned an empty response.");

            if (string.IsNullOrWhiteSpace(result.TrackingCode))
            {
                throw new DependencyUnavailableException("Coding service did not return a tracking code.");
            }

            return new CodingTrackingCodeResult(result.Id, result.TrackingCode, result.Counter);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Coding service unreachable.");
            throw new DependencyUnavailableException("Coding service is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Coding service timed out.");
            throw new DependencyUnavailableException("Coding service timed out.", ex);
        }
    }

    private sealed class CodingCreateResponse
    {
        public Guid Id { get; set; }

        [JsonPropertyName("trackingCode")]
        public string TrackingCode { get; set; } = string.Empty;

        public int Counter { get; set; }
    }
}
