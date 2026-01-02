namespace EnterpriseDataManager.Application.Validators;

using EnterpriseDataManager.Application.Commands.ArchivePlans;
using FluentValidation;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public sealed class CreateArchivePlanCommandValidator : AbstractValidator<CreateArchivePlanCommand>
{
    public CreateArchivePlanCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.SourcePath)
            .NotEmpty().WithMessage(PathCannotBeEmpty)
            .MaximumLength(2000).WithMessage("Source path cannot exceed 2000 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.SecurityLevel)
            .IsInEnum().WithMessage("Invalid security level.");

        RuleFor(x => x.Schedule)
            .MaximumLength(100).WithMessage("Schedule expression cannot exceed 100 characters.")
            .When(x => x.Schedule != null);
    }
}

public sealed class UpdateArchivePlanCommandValidator : AbstractValidator<UpdateArchivePlanCommand>
{
    public UpdateArchivePlanCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.SecurityLevel)
            .IsInEnum().WithMessage("Invalid security level.")
            .When(x => x.SecurityLevel.HasValue);
    }
}

public sealed class SetArchivePlanScheduleCommandValidator : AbstractValidator<SetArchivePlanScheduleCommand>
{
    public SetArchivePlanScheduleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.CronExpression)
            .NotEmpty().WithMessage(CronExpressionCannotBeEmpty)
            .MaximumLength(100).WithMessage("Cron expression cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => x.Description != null);
    }
}
