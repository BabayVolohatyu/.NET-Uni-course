namespace Catalog.Api.IntegrationEvents;

/// <summary>
/// Catalog (contractor registry) is the publisher for contractor facts.
/// The immutable event records live in <c>Platform.EventBus</c> so Tenders and Risk Assessments
/// deserialize the same contract. <c>ContractorService</c> calls <c>IEventPublisher</c> only after
/// <c>SaveChangesAsync</c> succeeds.
/// </summary>
public static class CatalogIntegrationEvents
{
    public const string Exchange = Platform.EventBus.MessagingTopology.CatalogExchange;
    public const string Registered = Platform.EventBus.MessagingTopology.RegisteredRoutingKey;
    public const string Renamed = Platform.EventBus.MessagingTopology.RenamedRoutingKey;
    public const string CapitalChanged = Platform.EventBus.MessagingTopology.CapitalChangedRoutingKey;
    public const string Removed = Platform.EventBus.MessagingTopology.RemovedRoutingKey;
}
