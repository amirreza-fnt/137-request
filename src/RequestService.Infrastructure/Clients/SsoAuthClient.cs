using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RequestService.Application.Exceptions;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Infrastructure.Clients;

/// <summary>
/// Validates a citizen/operator JWT against sso-login-service (GET /api/auth/me)
/// and extracts the identity. Resilient via the configured retry + circuit
/// breaker; a dead SSO produces a 503 (DependencyUnavailableException), never a
/// silent pass or a crash.
/// </summary>
public sealed class SsoAuthClient : ISsoAuthClient
{
    private readonly HttpClient _http;
    private readonly IOptions<SsoOptions> _options;
    private readonly ILogger<SsoAuthClient> _logger;

    public SsoAuthClient(HttpClient http, IOptions<SsoOptions> options, ILogger<SsoAuthClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<SsoUserInfo?> ValidateAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.Value.UserInfoPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogInformation("SSO token rejected with status {Status}.", response.StatusCode);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new DependencyUnavailableException(
                    $"SSO user-info endpoint returned HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<SsoMeResponse>(cancellationToken: ct);
            if (body is null || body.Success != true || body.Data is null)
            {
                return null;
            }

            var data = body.Data;
            if (string.IsNullOrWhiteSpace(data.MelliCode))
            {
                return null;
            }

            return new SsoUserInfo(data.Id ?? string.Empty, data.MelliCode, data.Phone);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "SSO service unreachable while validating token.");
            throw new DependencyUnavailableException("SSO service is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "SSO service timed out while validating token.");
            throw new DependencyUnavailableException("SSO service timed out.", ex);
        }
    }

    // Matches sso-login-service DTOs.Common.ApiResponse + DTOs.Auth.UserInfoDto (camelCase).
    private sealed class SsoMeResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public SsoMeData? Data { get; set; }
    }

    private sealed class SsoMeData
    {
        public string? Id { get; set; }
        public string? MelliCode { get; set; }
        public string? Phone { get; set; }
    }
}
