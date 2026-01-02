namespace EnterpriseDataManager.Application.Validators;

using EnterpriseDataManager.Application.Commands.ArchiveJobs;
using FluentValidation;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public sealed class CreateArchiveJobCommandValidator : AbstractValidator<CreateArchiveJobCommand>
{
    public CreateArchiveJobCommandValidator()
    {
        RuleFor(x => x.ArchivePlanId)
            .NotEmpty().WithMessage("Archive plan ID is required.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid job priority.");

        RuleFor(x => x.ScheduledAt)
            .Must(x => x == null || x > DateTimeOffset.UtcNow)
            .WithMessage("Scheduled time must be in the future.")
            .When(x => x.ScheduledAt.HasValue);

        RuleFor(x => x.TargetPath)
            .MaximumLength(2000).WithMessage("Target path cannot exceed 2000 characters.")
            .When(x => x.TargetPath != null);
    }
}

public sealed class FailArchiveJobCommandValidator : AbstractValidator<FailArchiveJobCommand>
{
    public FailArchiveJobCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(FailureReasonCannotBeEmpty)
            .MaximumLength(2000).WithMessage("Failure reason cannot exceed 2000 characters.");
    }
}

public sealed class AddArchiveItemCommandValidator : AbstractValidator<AddArchiveItemCommand>
{
    public AddArchiveItemCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("Job ID is required.");

        RuleFor(x => x.SourcePath)
            .NotEmpty().WithMessage(SourcePathCannotBeEmpty)
            .MaximumLength(2000).WithMessage("Source path cannot exceed 2000 characters.");

        RuleFor(x => x.TargetPath)
            .NotEmpty().WithMessage(TargetPathCannotBeEmpty)
            .MaximumLength(2000).WithMessage("Target path cannot exceed 2000 characters.");

        RuleFor(x => x.SizeBytes)
            .GreaterThanOrEqualTo(0).WithMessage(SizeCannotBeNegative);
    }
}

public sealed class RecordArchiveItemFailureCommandValidator : AbstractValidator<RecordArchiveItemFailureCommand>
{
    public RecordArchiveItemFailureCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("Job ID is required.");

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item ID is required.");

        RuleFor(x => x.Error)
            .NotEmpty().WithMessage(ErrorMessageCannotBeEmpty)
            .MaximumLength(2000).WithMessage("Error message cannot exceed 2000 characters.");
    }
}
