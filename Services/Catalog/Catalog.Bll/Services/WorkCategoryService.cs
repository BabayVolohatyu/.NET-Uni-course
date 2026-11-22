using AutoMapper;
using Catalog.Bll.DTOs;
using Catalog.Bll.Interfaces;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Models;

namespace Catalog.Bll.Services;

/// <summary>
/// Business-logic service for WorkCategory reference data.
/// Reference data is seeded via HasData migrations; this service provides read-only access.
/// </summary>
public sealed class WorkCategoryService : IWorkCategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public WorkCategoryService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<WorkCategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _uow.WorkCategories.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<WorkCategoryDto>>(categories);
    }

    public async Task<WorkCategoryDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var category = await _uow.WorkCategories.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(WorkCategory), id);
        return _mapper.Map<WorkCategoryDto>(category);
    }

    public async Task<WorkCategoryDto> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var category = await _uow.WorkCategories.GetByCodeAsync(code, ct)
            ?? throw new NotFoundException(nameof(WorkCategory), $"code '{code}'");
        return _mapper.Map<WorkCategoryDto>(category);
    }
}
