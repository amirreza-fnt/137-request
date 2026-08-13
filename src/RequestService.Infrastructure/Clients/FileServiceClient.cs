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
/// Checks that a FileId exists in the files service (GET /api/files/{id}).
/// Only metadata is returned — the file bytes are never transferred to
/// request-service.
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

    public async Task<FileMetadataDto?> GetFileAsync(string fileId, CancellationToken ct)
    {
        var path = _options.Value.GetFilePath.Replace("{id}", fileId);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        // Prefer X-Api-Key (internal). Fall back to Bearer JWT if the token looks like a JWT.
        if (!string.IsNullOrWhiteSpace(_options.Value.ServiceToken))
        {
            var token = _options.Value.ServiceToken.Trim();
            if (token.Count(c => c == '.') == 2)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", token);
            }
        }

        try
        {
            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Files service rejected the service token (HTTP {Status}).", response.StatusCode);
                throw new DependencyUnavailableException(
                    "Files service rejected the service token. Configure Files:ServiceToken (JWT signed with the files service key).");
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

            return new FileMetadataDto(info.Id, info.Extension, info.MimeType);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Files service unreachable while validating fileId {FileId}.", fileId);
            throw new DependencyUnavailableException("Files service is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Files service timed out while validating fileId {FileId}.", fileId);
            throw new DependencyUnavailableException("Files service timed out.", ex);
        }
    }

    // Matches FileStorage.Application.Dtos.FileInfoResponse (camelCase).
    private sealed class FileInfoResponse
    {
        public Guid Id { get; set; }
        public string? Extension { get; set; }
        public string? MimeType { get; set; }
    }
}
