using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>
/// Moves the current W3C trace context through RabbitMQ headers so one Aspire trace
/// spans the HTTP request, the publish, the consumer, and the database call.
/// </summary>
public static class TracePropagation
{
    public const string SourceName = "Platform.Messaging";

    public static readonly ActivitySource Source = new(SourceName);

    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public static void Inject(BasicProperties props)
    {
        if (Activity.Current is not { } activity)
            return;

        props.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            props.Headers,
            (headers, key, value) => headers[key] = Encoding.UTF8.GetBytes(value));
    }

    public static Activity? StartConsumerActivity(BasicDeliverEventArgs ea, string queue)
    {
        var parent = Propagator.Extract(
            default,
            ea.BasicProperties.Headers,
            (headers, key) =>
            {
                if (headers is null || !headers.TryGetValue(key, out var raw) || raw is null)
                    return [];

                var text = raw switch
                {
                    byte[] bytes => Encoding.UTF8.GetString(bytes),
                    string s => s,
                    _ => raw.ToString() ?? ""
                };
                return string.IsNullOrEmpty(text) ? [] : [text];
            });

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.operation.name", "process");
        activity?.SetTag("messaging.message.id", ea.BasicProperties.MessageId);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", ea.RoutingKey);
        return activity;
    }
}
