namespace RequestService.Application.Options;

/// <summary>Internal service-to-service authentication (telephony / internal callers).</summary>
public sealed class InternalAuthOptions
{
    public const string SectionName = "InternalAuth";

    public string HeaderName { get; set; } = "X-Api-Key";
    public List<InternalApiKey> ApiKeys { get; set; } = new();
}

public sealed class InternalApiKey
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
