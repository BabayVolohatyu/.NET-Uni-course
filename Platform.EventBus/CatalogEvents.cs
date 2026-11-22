namespace Platform.EventBus;

/// <summary>
/// Contractor registered in the registry (Catalog).
/// Consumers insert the local directory row. Bid snapshots are not created here.
/// </summary>
public record ContractorRegisteredEvent(
    long ContractorId,
    string ContractorName,
    string Edrpou,
    decimal AuthorizedCapital) : IntegrationEvent;

/// <summary>
/// Authorized capital changed. Domain analogue of ProductPriceChangedEvent:
/// Catalog is the owner; the local contractor directory is updated;
/// <c>Bids.ProposedPrice</c> (the placed-bid price) is a snapshot and must not change.
/// The payload is complete — consumers do not call Catalog back.
/// </summary>
public record ContractorCapitalChangedEvent(
    long ContractorId,
    string ContractorName,
    string Edrpou,
    decimal OldAuthorizedCapital,
    decimal NewAuthorizedCapital) : IntegrationEvent;

/// <summary>
/// Official company name changed. Domain analogue of ProductRenamedEvent.
/// Tenders updates <c>Contractors.Name</c>. Risk assessments update the denormalized
/// <c>contractorName</c> on every document that mentions this contractor.
/// <c>Bids.ContractorName</c> stays the name captured when the bid was submitted.
/// </summary>
public record ContractorRenamedEvent(
    long ContractorId,
    string OldName,
    string NewName,
    string Edrpou) : IntegrationEvent;

/// <summary>
/// Contractor removed from the registry. The local directory row is removed only when
/// no bid still references it (the bid table has a foreign key inside ProcurementBidding).
/// Bid snapshot columns are not rewritten.
/// </summary>
public record ContractorRemovedEvent(
    long ContractorId,
    string ContractorName,
    string Edrpou) : IntegrationEvent;
