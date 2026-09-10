using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RequestService.Application.Dtos;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Api.Controllers;

/// <summary>
/// Request lifecycle endpoints.
/// POST create, GET by id / tracking code / search are implemented;
/// status and refer remain reserved for a later phase.
/// </summary>
[ApiController]
[Route("api/v1/requests")]
[Produces("application/json")]
public sealed class RequestsController : ControllerBase
{
    private readonly IRequestService _requestService;
    private readonly IOptions<InternalAuthOptions> _internalAuth;

    public RequestsController(IRequestService requestService, IOptions<InternalAuthOptions> internalAuth)
    {
        _requestService = requestService;
        _internalAuth = internalAuth;
    }

    /// <summary>
    /// Registers a new citizen request and returns its id + unique tracking code.
    /// Authentication: SSO bearer token (citizen/operator) or X-Api-Key for
    /// internal-service channels (PhoneCall / InternalService).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CreateRequestResponse>> Create(
        [FromBody] CreateRequestRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var apiKey = Request.Headers[_internalAuth.Value.HeaderName].ToString();

        var response = await _requestService.CreateAsync(
            request,
            string.IsNullOrWhiteSpace(authorization) ? null : authorization,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Gets a single request by its internal id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RequestDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _requestService.GetByIdAsync(id, AuthHeader(), ApiKeyHeader(), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Gets a request by tracking code. Accepts the 5-digit code (e.g. <c>00042</c>)
    /// or digits-only TTS form (e.g. <c>42</c>).
    /// </summary>
    [HttpGet("by-tracking-code/{code}")]
    [ProducesResponseType(typeof(RequestDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailResponse>> GetByTrackingCode(
        string code,
        CancellationToken cancellationToken)
    {
        var response = await _requestService.GetByTrackingCodeAsync(
            code,
            AuthHeader(),
            ApiKeyHeader(),
            cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists recent requests (newest first, max 200). Filter by status / group / date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RequestDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RequestDetailResponse>>> Search(
        [FromQuery] string? status,
        [FromQuery] string? currentGroupId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var response = await _requestService.SearchAsync(
            status,
            currentGroupId,
            from,
            to,
            AuthHeader(),
            ApiKeyHeader(),
            cancellationToken);
        return Ok(response);
    }

    /// <summary>Changes the status of a request. TODO(next phase): implement.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ChangeStatus(Guid id, [FromBody] object body)
        => throw new NotImplementedException("PUT /api/v1/requests/{id}/status");

    /// <summary>Refers a request between groups. TODO(next phase): implement.</summary>
    [HttpPut("{id:guid}/refer")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Refer(Guid id, [FromBody] object body)
        => throw new NotImplementedException("PUT /api/v1/requests/{id}/refer");

    private string? AuthHeader()
    {
        var value = Request.Headers.Authorization.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string? ApiKeyHeader()
    {
        var value = Request.Headers[_internalAuth.Value.HeaderName].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
