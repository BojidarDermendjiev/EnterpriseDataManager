namespace EnterpriseDataManager.Application.Mapping;

using AutoMapper;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Services;

public sealed class StorageMappingProfile : Profile
{
    public StorageMappingProfile()
    {
        CreateMap<StorageProvider, StorageProviderDto>()
            .ForMember(dest => dest.HasCredentials, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.CredentialsReference)))
            .ForMember(dest => dest.ArchivePlanCount, opt => opt.MapFrom(src => src.ArchivePlans.Count));

        CreateMap<StorageProvider, StorageProviderSummaryDto>();

        CreateMap<StorageHealthStatus, StorageHealthDto>()
            .ForMember(dest => dest.ProviderName, opt => opt.Ignore());

        CreateMap<StorageUsageInfo, StorageUsageDto>()
            .ForMember(dest => dest.ProviderName, opt => opt.Ignore());
    }
}
