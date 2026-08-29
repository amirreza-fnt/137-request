using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RequestService.Application.Exceptions;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Api.Controllers;

/// <summary>Demo panel audio proxy — streams phone recordings without direct files-service JWT in the browser.</summary>
[ApiController]
[Route("api/v1/demo/files")]
public sealed class DemoFilesController : ControllerBase
{
    private readonly IFileServiceClient _files;
    private readonly IRequestRepository _requests;
    private readonly IOptions<InternalAuthOptions> _internalAuth;

    public DemoFilesController(
        IFileServiceClient files,
        IRequestRepository requests,
        IOptions<InternalAuthOptions> internalAuth)
    {
        _files = files;
        _requests = requests;
        _internalAuth = internalAuth;
    }

    /// <summary>GET /api/v1/demo/files/{fileId}/audio — play/download a request attachment.</summary>
    [HttpGet("{fileId}/audio")]
    public async Task<IActionResult> StreamAudio(
        string fileId,
        [FromQuery] string? key,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        if (!IsValidApiKey(Request.Headers[_internalAuth.Value.HeaderName].ToString(), key))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Valid X-Api-Key is required." });
        }

        if (!await _requests.RequestFileExistsAsync(fileId, cancellationToken))
        {
            return NotFound(new { code = "NOT_FOUND", message = "File is not linked to any request." });
        }

        try
        {
            var result = await _files.StreamFileAsync(fileId, cancellationToken);
            if (result is null)
            {
                return NotFound(new { code = "NOT_FOUND", message = "Audio file was not found in the files service." });
            }

            var contentType = result.ContentType ?? "audio/wav";
            if (!contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                contentType = "audio/wav";
            }

            Response.Headers.CacheControl = "private, max-age=300";
            if (download)
            {
                return File(result.Content, contentType, result.FileName);
            }

            Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
            return File(result.Content, contentType);
        }
        catch (DependencyUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "DEPENDENCY_UNAVAILABLE",
                message = ex.Message
            });
        }
    }

    private bool IsValidApiKey(string? headerValue, string? queryValue)
    {
        var candidate = !string.IsNullOrWhiteSpace(headerValue) ? headerValue.Trim() : queryValue?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return _internalAuth.Value.ApiKeys.Any(k =>
            !string.IsNullOrEmpty(k.Key) && k.Key.Equals(candidate, StringComparison.Ordinal));
    }
}
