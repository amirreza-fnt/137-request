namespace RequestService.Domain.Enums;

/// <summary>Who performed an action on a request.</summary>
public enum ActorType
{
    System,
    Operator,
    Citizen,
    ExternalService
}
