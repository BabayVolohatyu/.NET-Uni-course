using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Declares exchanges, queues, bindings, and dead-letter queues when the process starts,
/// before it accepts traffic. Repeating the declaration on the next start is safe.
/// </summary>
public sealed class MessagingTopologyHostedService(
    IConnection connection,
    ILogger<MessagingTopologyHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await MessagingTopology.DeclareAsync(channel, cancellationToken);
        logger.LogInformation(
            "Declared RabbitMQ topology: exchange {CatalogExchange} (topic), queues {TendersQueue} and {RiskQueue}, DLX/DLQ bound",
            MessagingTopology.CatalogExchange,
            MessagingTopology.TendersCatalogQueue,
            MessagingTopology.RiskCatalogQueue);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
