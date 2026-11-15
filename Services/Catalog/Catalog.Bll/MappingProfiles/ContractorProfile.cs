using AutoMapper;
using Catalog.Bll.DTOs;
using Catalog.Domain.Models;

namespace Catalog.Bll.MappingProfiles;

/// <summary>
/// AutoMapper profile for the Contractor aggregate and its related types.
/// Registered centrally in Program.cs via AddAutoMapper(typeof(ContractorProfile).Assembly).
/// </summary>
public sealed class ContractorProfile : Profile
{
    public ContractorProfile()
    {
        // Contractor → ContractorDto (summary, used in paged lists)
        CreateMap<Contractor, ContractorDto>();

        // Contractor → ContractorWithDetailsDto (full card)
        CreateMap<Contractor, ContractorWithDetailsDto>()
            .ForMember(dest => dest.WorkCategories,
                opt => opt.MapFrom(src =>
                    src.ContractorWorkCategories.Select(cwc => cwc.WorkCategory)));

        // Related entities
        CreateMap<ContractorDetail, ContractorDetailDto>();
        CreateMap<ContractorDocument, ContractorDocumentDto>();
        CreateMap<WorkCategory, WorkCategoryDto>();
    }
}
