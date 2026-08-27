using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RequestService.Application.Dtos;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;
namespace RequestService.Api.Controllers;

/// <summary>
/// Kartabl / operator phone-call APIs: list PhoneCall requests + live AMI control via IssabelBridge.
/// </summary>
[ApiController]
[Route("api/v1/phone-calls")]
[Produces("application/json")]
public sealed class PhoneCallsController : ControllerBase
{
    private readonly IRequestService _requestService;
    private readonly ITelephonyBridgeClient _bridge;
    private readonly IOptions<InternalAuthOptions> _internalAuth;
    private readonly IOptions<TelephonyOptions> _telephony;

    public PhoneCallsController(
        IRequestService requestService,
        ITelephonyBridgeClient bridge,
        IOptions<InternalAuthOptions> internalAuth,
        IOptions<TelephonyOptions> telephony)
    {
        _requestService = requestService;
        _bridge = bridge;
        _internalAuth = internalAuth;
        _telephony = telephony;
    }

    /// <summary>Lists recent PhoneCall requests (newest first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RequestDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RequestDetailResponse>>> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? outcome,
        CancellationToken cancellationToken)
    {
        var all = await _requestService.SearchAsync(
            status: null,
            currentGroupId: null,
            fromUtc: from,
            toUtc: to,
            authorizationHeader: AuthHeader(),
            apiKeyHeader: ApiKeyHeader(),
            ct: cancellationToken);

        IEnumerable<RequestDetailResponse> filtered = all.Where(r =>
            string.Equals(r.Channel, "PhoneCall", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(outcome))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.Outcome, outcome.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return Ok(filtered.ToList());
    }

    /// <summary>Live queue callers/members from Issabel AMI (via bridge).</summary>
    [HttpGet("live")]
    public async Task<IActionResult> Live([FromQuery] string? queue, CancellationToken cancellationToken)
    {
        RequireApiKey();
        if (string.IsNullOrWhiteSpace(_telephony.Value.BridgeBaseUrl))
        {
            return Ok(new
            {
                ok = false,
                configured = false,
                message = "Telephony:BridgeBaseUrl is empty on request-service.",
                callers = Array.Empty<object>(),
                members = Array.Empty<object>()
            });
        }

        var payload = await _bridge.GetActiveCallsAsync(queue, cancellationToken);
        return Ok(payload);
    }

    /// <summary>
    /// Operator control: answer (AMI Redirect to agent exten), reject, hangup.
    /// Body: { action, channel, exten?, context? }
    /// </summary>
    [HttpPost("control")]
    public async Task<IActionResult> Control([FromBody] PhoneCallControlRequest body, CancellationToken cancellationToken)
    {
        RequireApiKey();
        if (body is null || string.IsNullOrWhiteSpace(body.Action) || string.IsNullOrWhiteSpace(body.Channel))
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "action and channel are required." });
        }

        var action = body.Action.Trim().ToLowerInvariant();
        if (action is not ("answer" or "reject" or "hangup"))
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "action must be answer|reject|hangup." });
        }

        var payload = await _bridge.ControlAsync(
            action,
            body.Channel.Trim(),
            body.Exten,
            body.Context,
            cancellationToken);
        return Ok(payload);
    }

    /// <summary>Defaults for kartabl softphone / agent panel (incl. WebRTC).</summary>
    [HttpGet("config")]
    public IActionResult Config()
    {
        RequireApiKey();
        var t = _telephony.Value;
        var stun = (t.StunServers ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(new
        {
            queue = t.DefaultQueue,
            agentExten = string.IsNullOrWhiteSpace(t.DefaultAgentExten) ? "2101" : t.DefaultAgentExten,
            context = t.DefaultContext,
            bridgeConfigured = !string.IsNullOrWhiteSpace(t.BridgeBaseUrl)
                && !string.IsNullOrWhiteSpace(t.BridgeSecret),
            webrtc = new
            {
                enabled = !string.IsNullOrWhiteSpace(t.WssUrl)
                    && !string.IsNullOrWhiteSpace(t.SipDomain)
                    && !string.IsNullOrWhiteSpace(t.SipUsername)
                    && !string.IsNullOrWhiteSpace(t.SipPassword),
                wssUrl = t.WssUrl,
                sipDomain = t.SipDomain,
                sipUsername = t.SipUsername,
                sipPassword = t.SipPassword,
                sipUri = string.IsNullOrWhiteSpace(t.SipUsername) || string.IsNullOrWhiteSpace(t.SipDomain)
                    ? ""
                    : $"sip:{t.SipUsername}@{t.SipDomain}",
                stunServers = stun
            }
        });
    }

    private void RequireApiKey()
    {
        var key = ApiKeyHeader();
        if (string.IsNullOrWhiteSpace(key)
            || !_internalAuth.Value.ApiKeys.Any(k =>
                !string.IsNullOrWhiteSpace(k.Key)
                && string.Equals(k.Key, key, StringComparison.Ordinal)))
        {
            throw new RequestService.Application.Exceptions.NotAuthenticatedException(
                "Valid X-Api-Key is required for phone-call operator APIs.");
        }
    }

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

public sealed class PhoneCallControlRequest
{
    public string? Action { get; set; }
    public string? Channel { get; set; }
    public string? Exten { get; set; }
    public string? Context { get; set; }
}
