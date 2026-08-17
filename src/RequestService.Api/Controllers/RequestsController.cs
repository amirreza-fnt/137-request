using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RequestService.Application.Dtos;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;

namespace RequestService.Api.Controllers;

/// <summary>
/// Request lifecycle endpoints: create, read, status sync and group referral.
/// Status/refer are consumed by referral-service via X-Api-Key (Saga-lite outbox).
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
    /// After create, best-effort bootstraps the first referral workflow step.
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
        var authorization = Request.Headers.Authorization.ToString();
        var apiKey = Request.Headers[_internalAuth.Value.HeaderName].ToString();

        return Ok(await _requestService.GetByIdAsync(
            id,
            string.IsNullOrWhiteSpace(authorization) ? null : authorization,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            cancellationToken));
    }

    /// <summary>
    /// Gets a single request by tracking code (full <c>137-yyyyMMdd-000001</c>
    /// or digit-only form spoken by Issabel TTS).
    /// Public read for citizen / IVR tracking.
    /// </summary>
    [HttpGet("by-tracking-code/{code}")]
    [ProducesResponseType(typeof(RequestDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailResponse>> GetByTrackingCode(string code, CancellationToken cancellationToken)
        => Ok(await _requestService.GetByTrackingCodeAsync(code, cancellationToken));

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

    /// <summary>
    /// Changes the summary status of a request (used by referral-service outbox).
    /// Auth: X-Api-Key (preferred) or SSO bearer. Idempotent for the same status.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(UpdateRequestStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateRequestStatusResponse>> ChangeStatus(
        Guid id,
        [FromBody] UpdateRequestStatusRequest body,
        CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var apiKey = Request.Headers[_internalAuth.Value.HeaderName].ToString();

        return Ok(await _requestService.UpdateStatusAsync(
            id,
            body,
            string.IsNullOrWhiteSpace(authorization) ? null : authorization,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            cancellationToken));
    }

    /// <summary>
    /// Retargets the request to another group (sets CurrentGroupId + Status=Referred).
    /// Auth: X-Api-Key (preferred) or SSO bearer. Idempotent for the same group.
    /// </summary>
    [HttpPut("{id:guid}/refer")]
    [ProducesResponseType(typeof(ReferRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReferRequestResponse>> Refer(
        Guid id,
        [FromBody] ReferRequestRequest body,
        CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var apiKey = Request.Headers[_internalAuth.Value.HeaderName].ToString();

        return Ok(await _requestService.ReferAsync(
            id,
            body,
            string.IsNullOrWhiteSpace(authorization) ? null : authorization,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            cancellationToken));
    }
}
