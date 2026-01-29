using FluentValidation;
using SessionSight.Api.DTOs;

namespace SessionSight.Api.Validators;

public class CreateSessionValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.TherapistId).NotEmpty();
        RuleFor(x => x.SessionDate).NotEmpty();
        RuleFor(x => x.SessionType).IsInEnum();
        RuleFor(x => x.Modality).IsInEnum();
        RuleFor(x => x.DurationMinutes).GreaterThan(0).When(x => x.DurationMinutes.HasValue);
        RuleFor(x => x.SessionNumber).GreaterThanOrEqualTo(1);
    }
}
