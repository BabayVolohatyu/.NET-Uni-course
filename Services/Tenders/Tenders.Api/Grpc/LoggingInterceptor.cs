using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Diagnostics;

namespace Tenders.Api.Grpc;

/// <summary>
/// Server-side gRPC interceptor that logs every unary RPC call with:
///   • Method name and peer address (structured properties)
///   • Response status code and elapsed time in milliseconds
///   • Exception details on error (via Serilog enrichment)
///
/// Usage: builder.Services.AddGrpc(o => o.Interceptors.Add&lt;LoggingInterceptor&gt;())
/// CorrelationId is in the Serilog LogContext from CorrelationIdMiddleware automatically.
/// </summary>
public sealed class LoggingInterceptor(ILogger<LoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw     = Stopwatch.StartNew();
        var method = context.Method;
        var peer   = context.Peer;

        logger.LogInformation(
            "gRPC call started: Method={Method} Peer={Peer}", method, peer);

        try
        {
            var response = await continuation(request, context);

            logger.LogInformation(
                "gRPC call succeeded: Method={Method} Peer={Peer} Status={Status} ElapsedMs={ElapsedMs}",
                method, peer, context.Status.StatusCode, sw.ElapsedMilliseconds);

            return response;
        }
        catch (RpcException ex)
        {
            logger.LogWarning(
                ex,
                "gRPC call failed: Method={Method} Peer={Peer} Status={Status} ElapsedMs={ElapsedMs}",
                method, peer, ex.StatusCode, sw.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "gRPC call unexpected error: Method={Method} Peer={Peer} ElapsedMs={ElapsedMs}",
                method, peer, sw.ElapsedMilliseconds);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }
}
