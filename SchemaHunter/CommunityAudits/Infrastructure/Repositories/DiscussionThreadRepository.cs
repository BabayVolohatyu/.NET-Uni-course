using MongoDB.Driver;
using SchemaHunter.CommunityAudits.Domain.Documents;

namespace SchemaHunter.CommunityAudits.Infrastructure.Repositories;

/// <summary>
/// Repository for the <c>discussion_threads</c> MongoDB collection.
/// </summary>
public sealed class DiscussionThreadRepository
{
    private readonly IMongoCollection<DiscussionThread> _collection;

    public DiscussionThreadRepository(IMongoDatabase database)
        => _collection = database.GetCollection<DiscussionThread>("discussion_threads");

    /// <summary>Returns all (unlocked) threads for the given audit, newest first.</summary>
    public async Task<List<DiscussionThread>> GetByAuditAsync(
        string auditId, CancellationToken ct = default)
        => await _collection
            .Find(t => t.AuditId == auditId)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    /// <summary>Returns a single thread by its ObjectId.</summary>
    public async Task<DiscussionThread?> GetByIdAsync(
        string id, CancellationToken ct = default)
        => await _collection
            .Find(t => t.Id == id)
            .FirstOrDefaultAsync(ct);

    /// <summary>Creates a new discussion thread.</summary>
    public async Task InsertAsync(DiscussionThread thread, CancellationToken ct = default)
        => await _collection.InsertOneAsync(thread, cancellationToken: ct);

    /// <summary>Appends a comment to the thread's <c>comments</c> array.</summary>
    public async Task AddCommentAsync(
        string threadId, ThreadComment comment, CancellationToken ct = default)
    {
        var update = Builders<DiscussionThread>.Update
            .Push(t => t.Comments, comment)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(
            t => t.Id == threadId,
            update,
            cancellationToken: ct);
    }

    /// <summary>Locks a thread so no further comments can be added.</summary>
    public async Task LockAsync(string threadId, CancellationToken ct = default)
    {
        var update = Builders<DiscussionThread>.Update
            .Set(t => t.IsLocked, true)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(
            t => t.Id == threadId,
            update,
            cancellationToken: ct);
    }
}
