namespace Tenders.Domain.Constants;

/// <summary>
/// Allowed status values for a Tender in the ProcurementBidding context.
/// These match the CHECK constraint in db/p1/schema.sql.
/// </summary>
public static class TenderStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string BiddingOpen = "BiddingOpen";
    public const string EvaluationInProgress = "EvaluationInProgress";
    public const string Awarded = "Awarded";
    public const string Cancelled = "Cancelled";
}
