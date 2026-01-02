namespace EnterpriseDataManager.Application.Validators;

using EnterpriseDataManager.Application.Commands.StorageProviders;
using FluentValidation;
using static EnterpriseDataManager.Common.EntityValidationConstants;

public sealed class CreateLocalStorageProviderCommandValidator : AbstractValidator<CreateLocalStorageProviderCommand>
{
    public CreateLocalStorageProviderCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.RootPath)
            .NotEmpty().WithMessage("Root path is required for local storage.")
            .MaximumLength(2000).WithMessage("Root path cannot exceed 2000 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.QuotaBytes)
            .GreaterThan(0).WithMessage(QuotaCannotBeNegative)
            .When(x => x.QuotaBytes.HasValue);
    }
}

public sealed class CreateS3StorageProviderCommandValidator : AbstractValidator<CreateS3StorageProviderCommand>
{
    public CreateS3StorageProviderCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Endpoint is required for S3 storage.")
            .MaximumLength(2000).WithMessage("Endpoint cannot exceed 2000 characters.");

        RuleFor(x => x.Bucket)
            .NotEmpty().WithMessage("Bucket name is required for S3 storage.")
            .MaximumLength(63).WithMessage("Bucket name cannot exceed 63 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.QuotaBytes)
            .GreaterThan(0).WithMessage(QuotaCannotBeNegative)
            .When(x => x.QuotaBytes.HasValue);
    }
}

public sealed class CreateAzureBlobStorageProviderCommandValidator : AbstractValidator<CreateAzureBlobStorageProviderCommand>
{
    public CreateAzureBlobStorageProviderCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Endpoint is required for Azure Blob storage.")
            .MaximumLength(2000).WithMessage("Endpoint cannot exceed 2000 characters.");

        RuleFor(x => x.Container)
            .NotEmpty().WithMessage("Container name is required for Azure Blob storage.")
            .MaximumLength(63).WithMessage("Container name cannot exceed 63 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.QuotaBytes)
            .GreaterThan(0).WithMessage(QuotaCannotBeNegative)
            .When(x => x.QuotaBytes.HasValue);
    }
}

public sealed class UpdateStorageProviderCommandValidator : AbstractValidator<UpdateStorageProviderCommand>
{
    public UpdateStorageProviderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(NameCannotBeEmpty)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => x.Description != null);

        RuleFor(x => x.QuotaBytes)
            .GreaterThan(0).WithMessage(QuotaCannotBeNegative)
            .When(x => x.QuotaBytes.HasValue);
    }
}

public sealed class SetStorageQuotaCommandValidator : AbstractValidator<SetStorageQuotaCommand>
{
    public SetStorageQuotaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(IdCannotBeEmpty);

        RuleFor(x => x.QuotaBytes)
            .GreaterThan(0).WithMessage(QuotaCannotBeNegative)
            .When(x => x.QuotaBytes.HasValue);
    }
}
