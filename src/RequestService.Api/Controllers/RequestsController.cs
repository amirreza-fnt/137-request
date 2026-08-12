using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RequestService.Application.Dtos;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Api.Controllers;

/// <summary>
/// Request lifecycle endpoints.
/// POST /api/v1/requests is implemented (this phase); the remaining routes are
/// reserved for future phases and return 501 Not Implemented.
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

    // =====================================================================
    // Reserved endpoints for future phases (skeleton only).
    // =====================================================================

    /// <summary>Gets a single request by its internal id. TODO(next phase): implement.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetById(Guid id)
        => throw new NotImplementedException("GET /api/v1/requests/{id}");

    /// <summary>Gets a single request by its tracking code. TODO(next phase): implement.</summary>
    [HttpGet("by-tracking-code/{code}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetByTrackingCode(string code)
        => throw new NotImplementedException("GET /api/v1/requests/by-tracking-code/{code}");

    /// <summary>
    /// Searches the cartable by status, group and/or date range.
    /// TODO(next phase): implement.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Search(
        [FromQuery] string? status,
        [FromQuery] string? currentGroupId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        => throw new NotImplementedException("GET /api/v1/requests (search/cartable)");

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
}
