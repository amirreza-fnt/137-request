namespace RequestService.Domain.Enums;

/// <summary>Channel through which a citizen request enters the 137 system.</summary>
public enum RequestChannel
{
    PhoneCall,
    CitizenMobileApp,
    CitizenWebApp,
    OperatorApp,
    InternalService
}
