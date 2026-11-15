using Tenders.Bll.Dtos;

namespace Tenders.Bll.Interfaces;

/// <summary>
/// BLL service for Tender and ProcuringEntity operations.
/// Controllers call only this interface — they never touch repositories or UoW directly.
/// </summary>
public interface ITenderService
{
    // ── ProcuringEntity ─────────────────────────────────────────────────────────
    Task<ProcuringEntityDto?> GetProcuringEntityByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ProcuringEntityDto>> GetAllProcuringEntitiesAsync(CancellationToken ct = default);
    Task<ProcuringEntityDto> CreateProcuringEntityAsync(CreateProcuringEntityDto dto, CancellationToken ct = default);

    // ── Tender ───────────────────────────────────────────────────────────────────
    Task<TenderWithDetailsDto?> GetTenderByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<TenderDto>> GetTendersByEntityAsync(long procuringEntityId, string? status = null, CancellationToken ct = default);
    Task<TenderWithDetailsDto> CreateTenderAsync(CreateTenderDto dto, CancellationToken ct = default);
    Task ChangeTenderStatusAsync(long tenderId, string newStatus, string? reason = null, CancellationToken ct = default);
    Task<IReadOnlyList<TenderStatusHistoryDto>> GetStatusHistoryAsync(long tenderId, CancellationToken ct = default);
}
