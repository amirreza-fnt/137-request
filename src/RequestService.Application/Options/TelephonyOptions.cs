namespace RequestService.Application.Options;

public sealed class TelephonyOptions
{
    public const string SectionName = "Telephony";

    /// <summary>Base URL of IssabelBridge HTTP API on the PBX, e.g. http://192.168.1.70/137-bridge</summary>
    public string BridgeBaseUrl { get; set; } = "";

    /// <summary>Shared secret matching IssabelBridge config.php bridge_secret</summary>
    public string BridgeSecret { get; set; } = "";

    public string DefaultQueue { get; set; } = "8002";

    /// <summary>Agent dialed on "answer" — use 2101 for kartabl WebRTC (PJSIP).</summary>
    public string DefaultAgentExten { get; set; } = "2101";

    public string DefaultContext { get; set; } = "from-internal";

    public int TimeoutSeconds { get; set; } = 15;

    public bool AllowInvalidSslCertificate { get; set; } = true;

    /// <summary>JsSIP WebSocket URL, e.g. wss://192.168.1.70:8089/ws</summary>
    public string WssUrl { get; set; } = "";

    /// <summary>SIP domain / PBX host for URI, e.g. 192.168.1.70</summary>
    public string SipDomain { get; set; } = "";

    /// <summary>SIP auth username (usually same as DefaultAgentExten)</summary>
    public string SipUsername { get; set; } = "2101";

    /// <summary>SIP auth password for WebRTC endpoint</summary>
    public string SipPassword { get; set; } = "";

    /// <summary>Comma-separated STUN URLs. Leave empty on LAN (Google STUN often slow/blocked in IR).</summary>
    public string StunServers { get; set; } = "";
}
