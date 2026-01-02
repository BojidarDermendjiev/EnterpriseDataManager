namespace EnterpriseDataManager.Application.Mapping;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Services;

public sealed class AuditMappingProfile : Profile
{
    public AuditMappingProfile()
    {
        CreateMap<AuditRecord, AuditRecordDto>();

        CreateMap<AuditRecord, AuditRecordSummaryDto>();

        CreateMap<AuditSummary, AuditSummaryDto>()
            .ForMember(dest => dest.RecentFailures, opt => opt.MapFrom(src => src.RecentFailures));
    }
}
