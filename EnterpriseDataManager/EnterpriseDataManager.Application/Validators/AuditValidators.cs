namespace EnterpriseDataManager.Application.Validators;

using EnterpriseDataManager.Application.Commands.Audit;
using EnterpriseDataManager.Application.Queries.Audit;
using FluentValidation;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public sealed class CreateAuditRecordCommandValidator : AbstractValidator<CreateAuditRecordCommand>
{
    public CreateAuditRecordCommandValidator()
    {
        RuleFor(x => x.Actor)
            .NotEmpty().WithMessage(ActorCannotBeEmpty)
            .MaximumLength(256).WithMessage("Actor cannot exceed 256 characters.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage(ActionCannotBeEmpty)
            .MaximumLength(256).WithMessage("Action cannot exceed 256 characters.");

        RuleFor(x => x.ResourceType)
            .MaximumLength(256).WithMessage("Resource type cannot exceed 256 characters.")
            .When(x => x.ResourceType != null);

        RuleFor(x => x.ResourceId)
            .MaximumLength(256).WithMessage("Resource ID cannot exceed 256 characters.")
            .When(x => x.ResourceId != null);

        RuleFor(x => x.Details)
            .MaximumLength(4000).WithMessage("Details cannot exceed 4000 characters.")
            .When(x => x.Details != null);

        RuleFor(x => x.IpAddress)
            .MaximumLength(45).WithMessage("IP address cannot exceed 45 characters.")
            .When(x => x.IpAddress != null);

        RuleFor(x => x.UserAgent)
            .MaximumLength(1000).WithMessage("User agent cannot exceed 1000 characters.")
            .When(x => x.UserAgent != null);

        RuleFor(x => x.CorrelationId)
            .MaximumLength(64).WithMessage("Correlation ID cannot exceed 64 characters.")
            .When(x => x.CorrelationId != null);
    }
}

public sealed class PurgeOldAuditRecordsCommandValidator : AbstractValidator<PurgeOldAuditRecordsCommand>
{
    public PurgeOldAuditRecordsCommandValidator()
    {
        RuleFor(x => x.RetentionDays)
            .GreaterThan(0).WithMessage("Retention days must be positive.")
            .LessThanOrEqualTo(3650).WithMessage("Retention period cannot exceed 10 years.");
    }
}

public sealed class SearchAuditRecordsQueryValidator : AbstractValidator<SearchAuditRecordsQuery>
{
    public SearchAuditRecordsQueryValidator()
    {
        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0).WithMessage("Skip must be non-negative.");

        RuleFor(x => x.Take)
            .GreaterThan(0).WithMessage("Take must be positive.")
            .LessThanOrEqualTo(1000).WithMessage("Take cannot exceed 1000.");

        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To)
            .WithMessage("From date must be before To date.")
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}

public sealed class GetAuditSummaryQueryValidator : AbstractValidator<GetAuditSummaryQuery>
{
    public GetAuditSummaryQueryValidator()
    {
        RuleFor(x => x.From)
            .LessThan(x => x.To)
            .WithMessage("From date must be before To date.");
    }
}
