using AutoMapper;
using Tenders.Bll.Dtos;
using Tenders.Domain.Models;

namespace Tenders.Bll.Mapping;

/// <summary>
/// AutoMapper profile for the Tender aggregate.
/// Registered centrally in BLL and injected into DI via AddAutoMapper.
/// </summary>
public sealed class TenderProfile : Profile
{
    public TenderProfile()
    {
        CreateMap<Tender, TenderDto>();
        CreateMap<Tender, TenderWithDetailsDto>();
        CreateMap<TenderDetails, TenderDetailsDto>();
        CreateMap<TenderStatusHistory, TenderStatusHistoryDto>();
        CreateMap<ProcuringEntity, ProcuringEntityDto>();
    }
}
