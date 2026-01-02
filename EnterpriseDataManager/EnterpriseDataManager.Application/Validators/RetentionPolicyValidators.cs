namespace EnterpriseDataManager.Application.Validators;

using EnterpriseDataManager.Application.Commands.RetentionPolicies;
using FluentValidation;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public sealed class CreateRetentionPolicyCommandValidator : AbstractValidator<CreateRetentionPolicyCommand>
{
    public CreateRetentionPolicyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.RetentionDays)
            .GreaterThan(0).WithMessage(RetentionPeriodMustBePositive)
            .LessThanOrEqualTo(36500).WithMessage("Retention period cannot exceed 100 years.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.Scope)
            .MaximumLength(500).WithMessage("Scope cannot exceed 500 characters.")
            .When(x => x.Scope != null);
    }
}

public sealed class UpdateRetentionPolicyCommandValidator : AbstractValidator<UpdateRetentionPolicyCommand>
{
    public UpdateRetentionPolicyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.RetentionDays)
            .GreaterThan(0).WithMessage(RetentionPeriodMustBePositive)
            .LessThanOrEqualTo(36500).WithMessage("Retention period cannot exceed 100 years.")
            .When(x => x.RetentionDays.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.Scope)
            .MaximumLength(500).WithMessage("Scope cannot exceed 500 characters.")
            .When(x => x.Scope != null);
    }
}

public sealed class EnableLegalHoldCommandValidator : AbstractValidator<EnableLegalHoldCommand>
{
    public EnableLegalHoldCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.Reason)
            .MaximumLength(2000).WithMessage("Reason cannot exceed 2000 characters.")
            .When(x => x.Reason != null);
    }
}

public sealed class DisableLegalHoldCommandValidator : AbstractValidator<DisableLegalHoldCommand>
{
    public DisableLegalHoldCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.Reason)
            .MaximumLength(2000).WithMessage("Reason cannot exceed 2000 characters.")
            .When(x => x.Reason != null);
    }
}
