using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Broker topology declared in code at startup. Declaring the same durable objects again is safe.
/// <list type="bullet">
/// <item><c>catalog-events</c> — topic exchange. Catalog publishes; it does not know the consumers.</item>
/// <item><c>tenders.catalog-sync</c> — Bidding consumer (Orders analogue). Binding <c>contractor.*</c>.</item>
/// <item><c>risk-assessments.catalog-sync</c> — Risk consumer (Reviews analogue). Same binding, own queue (fan-out).</item>
/// <item>Each queue has its own fanout dead-letter exchange and DLQ, so a poison message stays with the service that failed it.</item>
/// <item><c>tenders-events</c> / <c>risk-assessments-events</c> — exchanges for facts those services publish.</item>
/// </list>
/// Several instances of one service share one queue (competing consumers).
/// </summary>
public static class MessagingTopology
{
    public const string CatalogExchange = "catalog-events";
    public const string TendersExchange = "tenders-events";
    public const string RiskAssessmentsExchange = "risk-assessments-events";

    public const string ContractorBinding = "contractor.*";

    public const string TendersCatalogQueue = "tenders.catalog-sync";
    public const string TendersCatalogDlx = "catalog-events.dlx";
    public const string TendersCatalogDlq = "tenders.catalog-sync.dlq";

    public const string RiskCatalogQueue = "risk-assessments.catalog-sync";
    public const string RiskCatalogDlx = "risk-assessments.catalog-sync.dlx";
    public const string RiskCatalogDlq = "risk-assessments.catalog-sync.dlq";

    public const string CapitalChangedRoutingKey = "contractor.capital_changed";
    public const string RenamedRoutingKey = "contractor.renamed";
    public const string RegisteredRoutingKey = "contractor.registered";
    public const string RemovedRoutingKey = "contractor.removed";
    public const string TenderStatusRoutingKey = "tender.status_changed";
    public const string AssessmentRecordedRoutingKey = "assessment.recorded";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(CatalogExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(TendersExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(RiskAssessmentsExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        await DeclareConsumerAsync(channel, TendersCatalogQueue, TendersCatalogDlx, TendersCatalogDlq, ct);
        await DeclareConsumerAsync(channel, RiskCatalogQueue, RiskCatalogDlx, RiskCatalogDlq, ct);
    }

    private static async Task DeclareConsumerAsync(
        IChannel channel, string queue, string dlx, string dlq, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(dlx, ExchangeType.Fanout, durable: true, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = dlx
            },
            cancellationToken: ct);
        await channel.QueueBindAsync(queue, CatalogExchange, routingKey: ContractorBinding, cancellationToken: ct);

        await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(dlq, dlx, routingKey: "", cancellationToken: ct);
    }
}
