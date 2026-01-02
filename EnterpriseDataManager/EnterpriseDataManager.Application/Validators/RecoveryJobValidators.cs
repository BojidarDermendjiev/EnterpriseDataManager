namespace EnterpriseDataManager.Application.Validators;

using EnterpriseDataManager.Application.Commands.RecoveryJobs;
using FluentValidation;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public sealed class CreateRecoveryJobCommandValidator : AbstractValidator<CreateRecoveryJobCommand>
{
    public CreateRecoveryJobCommandValidator()
    {
        RuleFor(x => x.ArchiveJobId)
            .NotEmpty().WithMessage(ArchiveJobIdCannotBeEmpty);

        RuleFor(x => x.DestinationPath)
            .NotEmpty().WithMessage(DestinationPathCannotBeEmpty)
            .MaximumLength(2000).WithMessage("Destination path cannot exceed 2000 characters.");
    }
}

public sealed class FailRecoveryJobCommandValidator : AbstractValidator<FailRecoveryJobCommand>
{
    public FailRecoveryJobCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(FailureReasonCannotBeEmpty)
            .MaximumLength(2000).WithMessage("Failure reason cannot exceed 2000 characters.");
    }
}
