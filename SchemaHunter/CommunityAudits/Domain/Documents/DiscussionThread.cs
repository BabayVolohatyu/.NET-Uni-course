using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SchemaHunter.CommunityAudits.Domain.Documents;

/// <summary>A reply nested inside a <see cref="ThreadComment"/>.</summary>
public class ThreadReply
{
    [BsonElement("replyId")]
    public string ReplyId { get; set; } = null!;

    [BsonElement("authorName")]
    public string AuthorName { get; set; } = null!;

    [BsonElement("body")]
    public string Body { get; set; } = null!;

    [BsonElement("postedAt")]
    public DateTime PostedAt { get; set; }
}

/// <summary>A top-level comment with optional nested replies.</summary>
public class ThreadComment
{
    [BsonElement("commentId")]
    public string CommentId { get; set; } = null!;

    [BsonElement("authorName")]
    public string AuthorName { get; set; } = null!;

    [BsonElement("body")]
    public string Body { get; set; } = null!;

    [BsonElement("postedAt")]
    public DateTime PostedAt { get; set; }

    [BsonElement("replies")]
    public List<ThreadReply> Replies { get; set; } = new();
}

/// <summary>The user who opened the discussion thread.</summary>
public class ThreadCreator
{
    [BsonElement("userId")]
    public string UserId { get; set; } = null!;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = null!;

    [BsonElement("orgName")]
    public string OrgName { get; set; } = null!;
}

/// <summary>
/// Secondary aggregate — discussion_threads collection.
/// Externally references <see cref="InspectionAudit"/> by ObjectId.
/// </summary>
[BsonIgnoreExtraElements]
public class DiscussionThread
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>External reference to <c>inspection_audits._id</c>.</summary>
    [BsonElement("auditId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AuditId { get; set; } = null!;

    [BsonElement("topicTitle")]
    public string TopicTitle { get; set; } = null!;

    [BsonElement("createdBy")]
    public ThreadCreator CreatedBy { get; set; } = null!;

    [BsonElement("comments")]
    public List<ThreadComment> Comments { get; set; } = new();

    [BsonElement("isLocked")]
    public bool IsLocked { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
