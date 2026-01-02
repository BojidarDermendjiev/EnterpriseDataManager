namespace EnterpriseDataManager.Application.Mapping;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;

public sealed class ArchiveMappingProfile : Profile
{
    public ArchiveMappingProfile()
    {
        CreateMap<ArchivePlan, ArchivePlanDto>()
            .ForMember(dest => dest.Schedule, opt => opt.MapFrom(src => src.Schedule != null ? src.Schedule.Expression : null))
            .ForMember(dest => dest.ScheduleDescription, opt => opt.MapFrom(src => src.Schedule != null ? src.Schedule.Description : null))
            .ForMember(dest => dest.SourcePath, opt => opt.MapFrom(src => src.SourcePath.Path))
            .ForMember(dest => dest.StorageProviderName, opt => opt.MapFrom(src => src.StorageProvider != null ? src.StorageProvider.Name : null))
            .ForMember(dest => dest.RetentionPolicyName, opt => opt.MapFrom(src => src.RetentionPolicy != null ? src.RetentionPolicy.Name : null))
            .ForMember(dest => dest.JobCount, opt => opt.MapFrom(src => src.ArchiveJobs.Count));

        CreateMap<ArchivePlan, ArchivePlanSummaryDto>();

        CreateMap<ArchiveJob, ArchiveJobDto>()
            .ForMember(dest => dest.ArchivePlanName, opt => opt.MapFrom(src => src.ArchivePlan != null ? src.ArchivePlan.Name : null))
            .ForMember(dest => dest.ProgressPercentage, opt => opt.MapFrom(src => src.GetProgressPercentage()))
            .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.GetDuration()));

        CreateMap<ArchiveJob, ArchiveJobSummaryDto>()
            .ForMember(dest => dest.ArchivePlanName, opt => opt.MapFrom(src => src.ArchivePlan != null ? src.ArchivePlan.Name : null))
            .ForMember(dest => dest.ProgressPercentage, opt => opt.MapFrom(src => src.GetProgressPercentage()));

        CreateMap<ArchiveJob, JobProgressDto>()
            .ForMember(dest => dest.JobId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.TotalItems, opt => opt.MapFrom(src => src.TotalItemCount))
            .ForMember(dest => dest.ProcessedItems, opt => opt.MapFrom(src => src.ProcessedItemCount))
            .ForMember(dest => dest.FailedItems, opt => opt.MapFrom(src => src.FailedItemCount))
            .ForMember(dest => dest.ProgressPercentage, opt => opt.MapFrom(src => src.GetProgressPercentage()))
            .ForMember(dest => dest.ElapsedTime, opt => opt.MapFrom(src => src.GetDuration()))
            .ForMember(dest => dest.EstimatedTimeRemaining, opt => opt.Ignore());

        CreateMap<ArchiveItem, ArchiveItemDto>();
    }
}
