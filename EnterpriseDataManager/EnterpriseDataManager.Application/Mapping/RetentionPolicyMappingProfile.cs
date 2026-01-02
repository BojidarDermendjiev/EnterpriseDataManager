namespace EnterpriseDataManager.Application.Mapping;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;

public sealed class RetentionPolicyMappingProfile : Profile
{
    public RetentionPolicyMappingProfile()
    {
        CreateMap<RetentionPolicy, RetentionPolicyDto>()
            .ForMember(dest => dest.RetentionDays, opt => opt.MapFrom(src => (int)src.RetentionPeriod.TotalDays))
            .ForMember(dest => dest.ArchivePlanCount, opt => opt.MapFrom(src => src.ArchivePlans.Count));

        CreateMap<RetentionPolicy, RetentionPolicySummaryDto>()
            .ForMember(dest => dest.RetentionDays, opt => opt.MapFrom(src => (int)src.RetentionPeriod.TotalDays));
    }
}
