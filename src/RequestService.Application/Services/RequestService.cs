using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RequestService.Application.Dtos;
using RequestService.Application.Exceptions;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;
using RequestService.Domain.Entities;
using RequestService.Domain.Enums;

namespace RequestService.Application.Services;

/// <summary>
/// Orchestrates the "register a citizen request" use case. All persistence is
/// wrapped by the repository in a single DB transaction.
/// </summary>
public sealed class RequestService : IRequestService
{
    private readonly IValidator<CreateRequestRequest> _validator;
    private readonly ISsoAuthClient _ssoAuthClient;
    private readonly IFileServiceClient _fileServiceClient;
    private readonly ITrackingCodeGenerator _trackingCodeGenerator;
    private readonly IRequestRepository _repository;
    private readonly IOptions<InternalAuthOptions> _internalAuthOptions;
    private readonly IOptions<FilesOptions> _filesOptions;
    private readonly ILogger<RequestService> _logger;

    public RequestService(
        IValidator<CreateRequestRequest> validator,
        ISsoAuthClient ssoAuthClient,
        IFileServiceClient fileServiceClient,
        ITrackingCodeGenerator trackingCodeGenerator,
        IRequestRepository repository,
        IOptions<InternalAuthOptions> internalAuthOptions,
        IOptions<FilesOptions> filesOptions,
        ILogger<RequestService> logger)
    {
        _validator = validator;
        _ssoAuthClient = ssoAuthClient;
        _fileServiceClient = fileServiceClient;
        _trackingCodeGenerator = trackingCodeGenerator;
        _repository = repository;
        _internalAuthOptions = internalAuthOptions;
        _filesOptions = filesOptions;
        _logger = logger;
    }

    public async Task<CreateRequestResponse> CreateAsync(
        CreateRequestRequest request,
        string? authorizationHeader,
        string? apiKeyHeader,
        CancellationToken ct)
    {
        // ---------- 1. Input validation ----------
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new DomainValidationException(
                string.Join(" | ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        // ---------- 2. Resolve caller (API key and/or SSO bearer token) ----------
        var caller = await ResolveCallerAsync(request.Channel, authorizationHeader, apiKeyHeader, ct);

        // ---------- 3. Determine the national code stored in Requests ----------
        var storedNationalCode = ResolveNationalCode(request, caller);

        // ---------- 4. Validate attached file ids against the files service ----------
        var fileEntities = await ValidateAndBuildFilesAsync(request.FileIds, ct);

        // ---------- 5. Unique tracking code ----------
        var trackingCode = await _trackingCodeGenerator.GenerateAsync(ct);
        if (await _repository.TrackingCodeExistsAsync(trackingCode, ct))
        {
            // Extremely unlikely with a monotonic sequence; retry once.
            trackingCode = await _trackingCodeGenerator.GenerateAsync(ct);
        }

        // ---------- 6. Build aggregates ----------
        var now = DateTime.UtcNow;
        var requestId = Guid.NewGuid();

        var entity = new Request
        {
            Id = requestId,
            TrackingCode = trackingCode,
            NationalCode = storedNationalCode,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            LocationLat = request.Location is null ? null : (decimal)request.Location.Lat,
            LocationLng = request.Location is null ? null : (decimal)request.Location.Lng,
            Channel = request.Channel,
            Status = RequestStatus.New,
            CurrentGroupId = null,
            CreatedBySourcePhone = ResolveSourcePhone(request, caller),
            CreatedAtUtc = now
        };

        var createdLog = new RequestLogItem
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            ActionType = RequestActionType.Created,
            ActorType = ToActorType(caller.Kind),
            ActorId = caller.ActorId,
            NewStatus = RequestStatus.New.ToString(),
            Description = BuildCreatedLogMetadata(request, caller),
            CreatedAtUtc = now
        };

        // Link navigations so EF Core orders the parent insert before children (FK ordering).
        foreach (var file in fileEntities)
        {
            file.Request = entity;
        }

        createdLog.Request = entity;

        // ---------- 7. Atomic persistence ----------
        await _repository.CreateAsync(entity, fileEntities, createdLog, ct);

        _logger.LogInformation(
            "Request {RequestId} created with tracking code {TrackingCode} via {Channel}",
            requestId, trackingCode, request.Channel);

        return new CreateRequestResponse(requestId, trackingCode);
    }

    private async Task<CallerContext> ResolveCallerAsync(
        RequestChannel channel,
        string? authorizationHeader,
        string? apiKeyHeader,
        CancellationToken ct)
    {
        var bearer = ExtractBearerToken(authorizationHeader);
        var apiKeyValid = IsValidApiKey(apiKeyHeader);

        // Internal service channel → API key is mandatory, citizen auth is optional.
        if (channel == RequestChannel.InternalService)
        {
            if (!apiKeyValid)
            {
                throw new NotAuthenticatedException("Internal service channel requires a valid X-Api-Key header.");
            }

            var keyName = ResolveApiKeyName(apiKeyHeader);
            return new CallerContext(CallerKind.ExternalService, keyName, null, null);
        }

        // Phone call (call center) → API key (IVR) or operator bearer token.
        if (channel == RequestChannel.PhoneCall)
        {
            if (apiKeyValid)
            {
                return new CallerContext(CallerKind.ExternalService, ResolveApiKeyName(apiKeyHeader), null, null);
            }

            if (!string.IsNullOrEmpty(bearer))
            {
                var sso = await ValidateSsoTokenAsync(bearer, ct);
                return new CallerContext(CallerKind.Operator, sso.MelliCode, sso.MelliCode, sso.Phone);
            }

            throw new NotAuthenticatedException("PhoneCall channel requires a valid X-Api-Key header or a bearer token.");
        }

        // Operator / citizen app channels → SSO bearer token is mandatory.
        if (string.IsNullOrEmpty(bearer))
        {
            throw new NotAuthenticatedException("An Authorization: Bearer token from sso-login-service is required.");
        }

        var userInfo = await ValidateSsoTokenAsync(bearer, ct);

        var kind = channel == RequestChannel.OperatorApp ? CallerKind.Operator : CallerKind.Citizen;
        return new CallerContext(kind, userInfo.MelliCode, userInfo.MelliCode, userInfo.Phone);
    }

    private async Task<SsoUserInfo> ValidateSsoTokenAsync(string token, CancellationToken ct)
    {
        var userInfo = await _ssoAuthClient.ValidateAsync(token, ct);
        if (userInfo is null || string.IsNullOrWhiteSpace(userInfo.MelliCode))
        {
            throw new NotAuthenticatedException("Invalid or expired token.");
        }

        return userInfo;
    }

    private static string? ResolveNationalCode(CreateRequestRequest request, CallerContext caller)
    {
        var bodyCode = NormalizeNationalCode(request.Citizen?.NationalCode);

        switch (request.Channel)
        {
            case RequestChannel.CitizenMobileApp:
            case RequestChannel.CitizenWebApp:
                // Token identity is authoritative; a conflicting body value is rejected by validation.
                return caller.MelliCode;

            case RequestChannel.OperatorApp:
            case RequestChannel.PhoneCall:
            case RequestChannel.InternalService:
            default:
                return bodyCode ?? caller.MelliCode;
        }
    }

    private static string? ResolveSourcePhone(CreateRequestRequest request, CallerContext caller)
    {
        if (request.Channel == RequestChannel.PhoneCall)
        {
            return !string.IsNullOrWhiteSpace(request.Citizen?.PhoneNumber)
                ? request.Citizen.PhoneNumber.Trim()
                : caller.Phone;
        }

        return null;
    }

    private static string? NormalizeNationalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private async Task<List<RequestFile>> ValidateAndBuildFilesAsync(
        List<string>? fileIds,
        CancellationToken ct)
    {
        var result = new List<RequestFile>();
        if (fileIds is null || fileIds.Count == 0)
        {
            return result;
        }

        foreach (var raw in fileIds)
        {
            var fileId = raw.Trim();

            var type = RequestFileType.Other;
            if (_filesOptions.Value.ValidationEnabled)
            {
                var meta = await _fileServiceClient.GetFileAsync(fileId, ct);
                if (meta is null)
                {
                    throw new DomainValidationException($"FileId '{fileId}' does not exist in the files service.");
                }

                type = ClassifyFileType(meta.Extension, meta.MimeType);
            }

            result.Add(new RequestFile
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                FileType = type,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        return result;
    }

    private static RequestFileType ClassifyFileType(string? extension, string? mimeType)
    {
        var probe = $"{extension} {mimeType}".ToLowerInvariant();

        if (probe.Contains("mp3") || probe.Contains("wav") || probe.Contains("ogg")
            || probe.Contains("audio") || probe.Contains("oga") || probe.Contains("m4a")
            || probe.Contains("flac") || probe.Contains("aac"))
        {
            return RequestFileType.Audio;
        }

        if (probe.Contains("mp4") || probe.Contains("webm") || probe.Contains("mkv")
            || probe.Contains("avi") || probe.Contains("mov") || probe.Contains("video"))
        {
            return RequestFileType.Video;
        }

        if (probe.Contains("jpg") || probe.Contains("jpeg") || probe.Contains("png")
            || probe.Contains("gif") || probe.Contains("webp") || probe.Contains("bmp")
            || probe.Contains("image"))
        {
            return RequestFileType.Image;
        }

        return RequestFileType.Other;
    }

    private static string? BuildCreatedLogMetadata(CreateRequestRequest request, CallerContext caller)
    {
        var meta = new Dictionary<string, string?>();

        // Temporary PII collected over the phone, stored only in the audit log
        // (NOT in Requests) until the citizen is registered in sso-login-service.
        var firstName = request.Citizen?.FirstName;
        var lastName = request.Citizen?.LastName;
        var phone = request.Citizen?.PhoneNumber ?? caller.Phone;

        if (!string.IsNullOrWhiteSpace(firstName)) meta["firstName"] = firstName.Trim();
        if (!string.IsNullOrWhiteSpace(lastName)) meta["lastName"] = lastName.Trim();
        if (!string.IsNullOrWhiteSpace(phone)) meta["phoneNumber"] = phone.Trim();
        meta["channel"] = request.Channel.ToString();

        if (meta.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(meta);
    }

    private bool IsValidApiKey(string? apiKeyHeader)
    {
        if (string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return false;
        }

        return _internalAuthOptions.Value.ApiKeys.Any(k =>
            !string.IsNullOrEmpty(k.Key) && k.Key.Equals(apiKeyHeader.Trim(), StringComparison.Ordinal));
    }

    private string? ResolveApiKeyName(string? apiKeyHeader)
    {
        return _internalAuthOptions.Value.ApiKeys
            .FirstOrDefault(k => k.Key.Equals(apiKeyHeader?.Trim(), StringComparison.Ordinal))
            ?.Name;
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string prefix = "Bearer ";
        if (authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorizationHeader[prefix.Length..].Trim();
        }

        return null;
    }

    private static ActorType ToActorType(CallerKind kind) => kind switch
    {
        CallerKind.Citizen => ActorType.Citizen,
        CallerKind.Operator => ActorType.Operator,
        CallerKind.ExternalService => ActorType.ExternalService,
        _ => ActorType.System
    };
}
