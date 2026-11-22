using AutoMapper;
using Tenders.Bll.Dtos;
using Tenders.Domain.Models;

namespace Tenders.Bll.Mapping;

/// <summary>
/// AutoMapper profile for Bid objects.
/// </summary>
public sealed class BidProfile : Profile
{
    public BidProfile()
    {
        CreateMap<Bid, BidDto>();
    }
}
