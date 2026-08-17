using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RequestService.Application.Exceptions;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Infrastructure.Clients;

/// <summary>
/// Checks that a FileId exists in the files service and can stream bytes for the demo panel.
/// </summary>
public sealed class FileServiceClient : IFileServiceClient
{
    private readonly HttpClient _http;
    private readonly IOptions<FilesOptions> _options;
    private readonly ILogger<FileServiceClient> _logger;

    public FileServiceClient(HttpClient http, IOptions<FilesOptions> options, ILogger<FileServiceClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public Task<FileMetadataDto?> GetFileAsync(string fileId, CancellationToken ct)
        => GetFileInternalAsync(fileId, ct);

    public async Task<FileStreamResult?> StreamFileAsync(string fileId, CancellationToken ct)
    {
        var meta = await GetFileInternalAsync(fileId, ct);
        if (meta is null || string.IsNullOrWhiteSpace(meta.ShortCode))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/i/{meta.ShortCode}");
        ApplyAuth(request);

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new DependencyUnavailableException(
                $"Files service returned HTTP {(int)response.StatusCode} while streaming '{fileId}'.");
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? meta.MimeType ?? "audio/wav";
        var fileName = $"recording-{fileId}{meta.Extension ?? ".wav"}";
        return new FileStreamResult(stream, contentType, fileName);
    }

    private async Task<FileMetadataDto?> GetFileInternalAsync(string fileId, CancellationToken ct)
    {
        if (!Guid.TryParse(fileId, out _))
        {
            return null;
        }

        var path = _options.Value.GetFilePath.Replace("{id}", fileId);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuth(request);

        try
        {
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Files service rejected auth while reading {FileId} (HTTP {Status}).", fileId, response.StatusCode);
                throw new DependencyUnavailableException(
                    "Files service rejected auth. Configure Files:JwtKey (or Files:ServiceToken).");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new DependencyUnavailableException(
                    $"Files service returned HTTP {(int)response.StatusCode} for file '{fileId}'.");
            }

            var info = await response.Content.ReadFromJsonAsync<FileInfoResponse>(cancellationToken: ct);
            if (info is null || info.Id == Guid.Empty)
            {
                return null;
            }

            return new FileMetadataDto(
                info.Id,
                info.Extension,
                info.MimeType,
                info.ShortCode,
                info.AccessType);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Files service unreachable while reading fileId {FileId}.", fileId);
            throw new DependencyUnavailableException("Files service is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Files service timed out while reading fileId {FileId}.", fileId);
            throw new DependencyUnavailableException("Files service timed out.", ex);
        }
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.Value.ServiceToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Value.ServiceToken);
            return;
        }

        var token = CreateAdminJwt();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private string? CreateAdminJwt()
    {
        var opts = _options.Value;
        if (string.IsNullOrWhiteSpace(opts.JwtKey))
        {
            return null;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: opts.JwtIssuer,
            audience: opts.JwtAudience,
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
            },
            notBefore: now,
            expires: now.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FileInfoResponse
    {
        public Guid Id { get; set; }
        public string? ShortCode { get; set; }
        public string? Extension { get; set; }
        public string? MimeType { get; set; }
        public string? AccessType { get; set; }
    }
}
