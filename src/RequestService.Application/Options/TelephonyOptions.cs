namespace RequestService.Application.Options;

public sealed class TelephonyOptions
{
    public const string SectionName = "Telephony";

    /// <summary>Base URL of IssabelBridge HTTP API on the PBX, e.g. http://192.168.1.70/137-bridge</summary>
    public string BridgeBaseUrl { get; set; } = "";

    /// <summary>Shared secret matching IssabelBridge config.php bridge_secret</summary>
    public string BridgeSecret { get; set; } = "";

    public string DefaultQueue { get; set; } = "8002";

    public string DefaultAgentExten { get; set; } = "2001";

    public string DefaultContext { get; set; } = "from-internal";

    public int TimeoutSeconds { get; set; } = 8;

    public bool AllowInvalidSslCertificate { get; set; } = true;
}
