using MongoDB.Bson;

namespace RiskAssessments.Domain.Common;

/// <summary>
/// MongoDB document root. <see cref="Id"/> is stored as <c>_id</c>.
/// <see cref="Version"/> is the optimistic-concurrency token (ETag / If-Match).
/// </summary>
public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; protected set; }

    public int Version { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    public void BumpVersion() => Version++;
}
