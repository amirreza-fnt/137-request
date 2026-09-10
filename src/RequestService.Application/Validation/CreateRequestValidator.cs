using FluentValidation;
using RequestService.Application.Dtos;
using RequestService.Domain.Enums;

namespace RequestService.Application.Validation;

/// <summary>
/// Channel-aware validation of the create-request payload:
///   - PhoneCall / InternalService: identity, description and location may be empty.
///   - CitizenMobileApp / CitizenWebApp: location is required.
///   - OperatorApp: national code of the citizen is required.
/// </summary>
public sealed class CreateRequestValidator : AbstractValidator<CreateRequestRequest>
{
    private static readonly string[] VideoExtensions =
        { ".mp4", ".webm", ".mkv", ".avi", ".mov" };

    public CreateRequestValidator()
    {
        RuleFor(x => x.Channel)
            .IsInEnum()
            .WithMessage("Channel must be one of: PhoneCall, CitizenMobileApp, CitizenWebApp, OperatorApp, InternalService.");

        // ---- Location ----
        RuleFor(x => x.Location)
            .NotNull()
            .WithMessage("Location is required for this channel.")
            .When(x => x.Channel is RequestChannel.CitizenMobileApp or RequestChannel.CitizenWebApp);

        RuleFor(x => x.Location!.Lat)
            .InclusiveBetween(-90, 90)
            .WithMessage("Location.Lat must be between -90 and 90.")
            .When(x => x.Location is not null);

        RuleFor(x => x.Location!.Lng)
            .InclusiveBetween(-180, 180)
            .WithMessage("Location.Lng must be between -180 and 180.")
            .When(x => x.Location is not null);

        // ---- National code ----
        RuleFor(x => x.Citizen!.NationalCode)
            .NotNull()
            .WithMessage("Citizen.NationalCode is required when an operator registers on behalf of a citizen.")
            .When(x => x.Channel == RequestChannel.OperatorApp);

        RuleFor(x => x.Citizen!.NationalCode)
            .NotEmpty()
            .WithMessage("Citizen.NationalCode is required to allocate a tracking code.")
            .When(x => x.Channel is RequestChannel.PhoneCall
                or RequestChannel.OperatorApp
                or RequestChannel.InternalService);

        RuleFor(x => x.Citizen!.NationalCode)
            .Matches("^[0-9]{10}$")
            .WithMessage("Citizen.NationalCode must be exactly 10 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.Citizen?.NationalCode));

        // ---- Description ----
        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Description must not exceed 2000 characters.");

        // ---- Identity extras ----
        RuleFor(x => x.Citizen!.FirstName)
            .MaximumLength(100)
            .WithMessage("Citizen.FirstName must not exceed 100 characters.")
            .When(x => x.Citizen is not null);

        RuleFor(x => x.Citizen!.LastName)
            .MaximumLength(100)
            .WithMessage("Citizen.LastName must not exceed 100 characters.")
            .When(x => x.Citizen is not null);

        RuleFor(x => x.Citizen!.PhoneNumber)
            .Matches("^[0-9+ -]{7,20}$")
            .WithMessage("Citizen.PhoneNumber is not a valid phone number.")
            .When(x => !string.IsNullOrWhiteSpace(x.Citizen?.PhoneNumber));

        // ---- Files ----
        RuleFor(x => x.FileIds)
            .Must(f => f!.Count <= 20)
            .WithMessage("A request can reference at most 20 files.")
            .When(x => x.FileIds is not null);

        RuleForEach(x => x.FileIds)
            .Must(BeAValidFileId)
            .WithMessage("Each fileId must be a valid GUID (as issued by the files service).")
            .When(x => x.FileIds is not null);

        RuleFor(x => x.FileIds)
            .Must(f => f!.Distinct().Count() == f!.Count)
            .WithMessage("FileIds must not contain duplicates.")
            .When(x => x.FileIds is not null);
    }

    private static bool BeAValidFileId(string fileId)
        => Guid.TryParse(fileId, out _) && fileId.Length == 36;
}
