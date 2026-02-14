using FluentValidation;
using SessionSight.Api.DTOs;

namespace SessionSight.Api.Validators;

public class CreateTherapistValidator : AbstractValidator<CreateTherapistRequest>
{
    public CreateTherapistValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LicenseNumber).MaximumLength(50);
        RuleFor(x => x.Credentials).MaximumLength(50);
    }
}
