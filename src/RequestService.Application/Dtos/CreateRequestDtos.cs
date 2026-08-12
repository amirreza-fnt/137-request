using RequestService.Domain.Enums;

namespace RequestService.Application.Dtos;

/// <summary>Citizen identity as submitted by the caller (may be partially filled in telephony scenarios).</summary>
public sealed record CitizenDto(
    string? NationalCode,
    string? FirstName,
    string? LastName,
    string? PhoneNumber);

public sealed record LocationDto(double Lat, double Lng);

public sealed record CreateRequestRequest(
    CitizenDto? Citizen,
    string? Description,
    LocationDto? Location,
    List<string>? FileIds,
    RequestChannel Channel);

public sealed record CreateRequestResponse(Guid RequestId, string TrackingCode);
