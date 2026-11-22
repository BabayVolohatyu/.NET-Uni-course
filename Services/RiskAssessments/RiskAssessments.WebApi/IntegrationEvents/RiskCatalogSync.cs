using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Platform.EventBus;
using RiskAssessments.Infrastructure.Persistence;

namespace RiskAssessments.WebApi.IntegrationEvents;

/// <summary>
/// Applies a catalog event to risk-assessment documents and records <c>EventId</c>
/// in the same MongoDB transaction when the server supports transactions.
/// On a standalone node the insert is compensated if the document update fails,
/// so a retry can run the change again.
/// </summary>
public sealed class RiskCatalogSync(
    MongoDbContext context,
    IMemoryCache memoryCache,
    IDistributedCache distributedCache,
    ILogger<RiskCatalogSync> logger)
{
    public async Task ExecuteAsync(
        IntegrationEvent integrationEvent,
        long contractorId,
        string stateField,
        Func<IClientSessionHandle?, CancellationToken, Task> mutate,
        CancellationToken ct)
    {
        try
        {
            await ExecuteInTransactionAsync(integrationEvent, contractorId, stateField, mutate, ct);
        }
        catch (Exception ex) when (IsReplicaSetRequired(ex))
        {
            logger.LogWarning(
                "MongoDB transactions are unavailable on this server. Applying {EventId} with a compensating processed-event delete if the update fails",
                integrationEvent.EventId);
            await ExecuteWithCompensationAsync(integrationEvent, contractorId, stateField, mutate, ct);
        }
    }

    public async Task<long> SetContractorNameAsync(
        IClientSessionHandle? session,
        long contractorId,
        string contractorName,
        bool active,
        CancellationToken ct)
    {
        var assessments = context.Database.GetCollection<BsonDocument>("riskAssessments");
        var filter = ContractorFilter(contractorId);
        var update = Builders<BsonDocument>.Update
            .Set("triggeredFlags.$[flag].contractorName", contractorName)
            .Set("triggeredFlags.$[flag].contractorActive", active)
            .Set("updatedAt", DateTime.UtcNow);
        var options = new UpdateOptions
        {
            ArrayFilters = [ArrayFilter(contractorId)]
        };

        var result = session is null
            ? await assessments.UpdateManyAsync(filter, update, options, ct)
            : await assessments.UpdateManyAsync(session, filter, update, options, ct);

        await InvalidateCachedAssessmentsAsync(session, filter, ct);
        return result.ModifiedCount;
    }

    private async Task ExecuteInTransactionAsync(
        IntegrationEvent integrationEvent,
        long contractorId,
        string stateField,
        Func<IClientSessionHandle?, CancellationToken, Task> mutate,
        CancellationToken ct)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            await InsertProcessedAsync(session, integrationEvent, ct);
            if (await IsStaleAsync(session, contractorId, stateField, integrationEvent.OccurredAt, ct))
            {
                await session.CommitTransactionAsync(ct);
                logger.LogInformation(
                    "Out-of-order event {EventId} EventType={EventType} CorrelationId={CorrelationId} ContractorId={ContractorId} ignored; a newer {StateField} is already applied",
                    integrationEvent.EventId, integrationEvent.GetType().Name, integrationEvent.CorrelationId, contractorId, stateField);
                return;
            }

            await mutate(session, ct);
            await TouchStateAsync(session, contractorId, stateField, integrationEvent.OccurredAt, ct);
            await session.CommitTransactionAsync(ct);
        }
        catch (MongoWriteException ex) when (IsDuplicate(ex))
        {
            logger.LogInformation(
                "Duplicate event {EventId} EventType={EventType} CorrelationId={CorrelationId} skipped",
                integrationEvent.EventId, integrationEvent.GetType().Name, integrationEvent.CorrelationId);
        }
    }

    private async Task ExecuteWithCompensationAsync(
        IntegrationEvent integrationEvent,
        long contractorId,
        string stateField,
        Func<IClientSessionHandle?, CancellationToken, Task> mutate,
        CancellationToken ct)
    {
        try
        {
            await InsertProcessedAsync(session: null, integrationEvent, ct);
        }
        catch (MongoWriteException ex) when (IsDuplicate(ex))
        {
            logger.LogInformation(
                "Duplicate event {EventId} EventType={EventType} CorrelationId={CorrelationId} skipped",
                integrationEvent.EventId, integrationEvent.GetType().Name, integrationEvent.CorrelationId);
            return;
        }

        try
        {
            if (await IsStaleAsync(session: null, contractorId, stateField, integrationEvent.OccurredAt, ct))
            {
                logger.LogInformation(
                    "Out-of-order event {EventId} EventType={EventType} ContractorId={ContractorId} ignored",
                    integrationEvent.EventId, integrationEvent.GetType().Name, contractorId);
                return;
            }

            await mutate(null, ct);
            await TouchStateAsync(session: null, contractorId, stateField, integrationEvent.OccurredAt, ct);
        }
        catch
        {
            var processed = context.Database.GetCollection<BsonDocument>("processedEvents");
            await processed.DeleteOneAsync(
                Builders<BsonDocument>.Filter.Eq("eventId", integrationEvent.EventId.ToString()),
                ct);
            throw;
        }
    }

    private async Task InsertProcessedAsync(
        IClientSessionHandle? session,
        IntegrationEvent integrationEvent,
        CancellationToken ct)
    {
        var processed = context.Database.GetCollection<BsonDocument>("processedEvents");
        var document = new BsonDocument
        {
            ["eventId"] = integrationEvent.EventId.ToString(),
            ["eventType"] = integrationEvent.GetType().Name,
            ["correlationId"] = integrationEvent.CorrelationId is null
                ? BsonNull.Value
                : integrationEvent.CorrelationId,
            ["processedAt"] = DateTime.UtcNow
        };

        if (session is null)
            await processed.InsertOneAsync(document, cancellationToken: ct);
        else
            await processed.InsertOneAsync(session, document, cancellationToken: ct);
    }

    private async Task<bool> IsStaleAsync(
        IClientSessionHandle? session,
        long contractorId,
        string stateField,
        DateTime occurredAt,
        CancellationToken ct)
    {
        var state = context.Database.GetCollection<BsonDocument>("catalogSyncState");
        var filter = Builders<BsonDocument>.Filter.Eq("contractorId", contractorId);
        var row = session is null
            ? await state.Find(filter).FirstOrDefaultAsync(ct)
            : await state.Find(session, filter).FirstOrDefaultAsync(ct);

        if (row is null || !row.TryGetValue(stateField, out var raw) || !raw.IsValidDateTime)
            return false;

        return raw.ToUniversalTime() > occurredAt.ToUniversalTime();
    }

    private async Task TouchStateAsync(
        IClientSessionHandle? session,
        long contractorId,
        string stateField,
        DateTime occurredAt,
        CancellationToken ct)
    {
        var state = context.Database.GetCollection<BsonDocument>("catalogSyncState");
        var filter = Builders<BsonDocument>.Filter.Eq("contractorId", contractorId);
        var update = Builders<BsonDocument>.Update
            .Set("contractorId", contractorId)
            .Set(stateField, occurredAt.ToUniversalTime());
        var options = new UpdateOptions { IsUpsert = true };
        if (session is null)
            await state.UpdateOneAsync(filter, update, options, ct);
        else
            await state.UpdateOneAsync(session, filter, update, options, ct);
    }

    private async Task InvalidateCachedAssessmentsAsync(
        IClientSessionHandle? session,
        FilterDefinition<BsonDocument> filter,
        CancellationToken ct)
    {
        var assessments = context.Database.GetCollection<BsonDocument>("riskAssessments");
        var query = session is null ? assessments.Find(filter) : assessments.Find(session, filter);
        var ids = await query.Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync(ct);
        foreach (var document in ids)
        {
            var key = $"risk:assessment:{document["_id"]}";
            memoryCache.Remove(key);
            await distributedCache.RemoveAsync(key, ct);
            logger.LogInformation("Invalidated risk assessment cache key {CacheKey} after catalog event", key);
        }
    }

    private static FilterDefinition<BsonDocument> ContractorFilter(long contractorId)
    {
        var ids = new BsonArray { contractorId };
        if (contractorId is >= int.MinValue and <= int.MaxValue)
            ids.Add((int)contractorId);

        return new BsonDocument("triggeredFlags.contractorId", new BsonDocument("$in", ids));
    }

    private static BsonDocumentArrayFilterDefinition<BsonDocument> ArrayFilter(long contractorId)
    {
        var ids = new BsonArray { contractorId };
        if (contractorId is >= int.MinValue and <= int.MaxValue)
            ids.Add((int)contractorId);

        return new BsonDocumentArrayFilterDefinition<BsonDocument>(
            new BsonDocument("flag.contractorId", new BsonDocument("$in", ids)));
    }

    private static bool IsDuplicate(MongoWriteException ex) =>
        ex.WriteError.Category == ServerErrorCategory.DuplicateKey || ex.WriteError.Code == 11000;

    private static bool IsReplicaSetRequired(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is MongoCommandException cmd &&
                (cmd.Code == 20 || cmd.CodeName.Contains("IllegalOperation", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (current.Message.Contains("replica set", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Transaction numbers are only allowed", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
