namespace EnterpriseDataManager.Application.Mapping;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;

public sealed class RecoveryMappingProfile : Profile
{
    public RecoveryMappingProfile()
    {
        CreateMap<RecoveryJob, RecoveryJobDto>()
            .ForMember(dest => dest.ProgressPercentage, opt => opt.MapFrom(src => src.GetProgressPercentage()))
            .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.GetDuration()));

        CreateMap<RecoveryJob, RecoveryJobSummaryDto>()
            .ForMember(dest => dest.ProgressPercentage, opt => opt.MapFrom(src => src.GetProgressPercentage()));
    }
}
