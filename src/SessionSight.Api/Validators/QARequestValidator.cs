using FluentValidation;
using SessionSight.Agents.Models;

namespace SessionSight.Api.Validators;

public class QARequestValidator : AbstractValidator<QARequest>
{
    public QARequestValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
    }
}
