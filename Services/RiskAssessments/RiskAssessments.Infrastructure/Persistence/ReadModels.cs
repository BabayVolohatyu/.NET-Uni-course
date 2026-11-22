using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RiskAssessments.Infrastructure.Persistence;

[BsonIgnoreExtraElements]
internal sealed class RiskAssessmentReadModel
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("tenderId")]
    public long TenderId { get; set; }

    [BsonElement("tenderTitle")]
    public string TenderTitle { get; set; } = "";

    [BsonElement("procuringEntityId")]
    public long ProcuringEntityId { get; set; }

    [BsonElement("procuringEntityName")]
    public string ProcuringEntityName { get; set; } = "";

    [BsonElement("auditor")]
    public AuditorReadModel? Auditor { get; set; }

    [BsonElement("assessedBy")]
    public string? AssessedBy { get; set; }

    [BsonElement("overallRiskScore")]
    public double OverallRiskScore { get; set; }

    [BsonElement("riskLevel")]
    public string RiskLevel { get; set; } = "";

    [BsonElement("expectedBudget")]
    public MoneyReadModel? ExpectedBudget { get; set; }

    [BsonElement("triggeredFlags")]
    public List<TriggeredFlagReadModel> TriggeredFlags { get; set; } = [];

    [BsonElement("auditNoteIds")]
    public List<ObjectId> AuditNoteIds { get; set; } = [];

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

[BsonIgnoreExtraElements]
internal sealed class AuditorReadModel
{
    [BsonElement("userId")]
    public string UserId { get; set; } = "";

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = "";
}

[BsonIgnoreExtraElements]
internal sealed class MoneyReadModel
{
    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "";
}

[BsonIgnoreExtraElements]
internal sealed class TriggeredFlagReadModel
{
    [BsonElement("ruleCode")]
    public string RuleCode { get; set; } = "";

    [BsonElement("severity")]
    public string Severity { get; set; } = "";

    [BsonElement("description")]
    public string Description { get; set; } = "";

    [BsonElement("detectedAt")]
    public DateTime DetectedAt { get; set; }

    [BsonElement("affectedField")]
    public string? AffectedField { get; set; }

    [BsonElement("contractorId")]
    public long? ContractorId { get; set; }

    [BsonElement("companyAgeDays")]
    public int? CompanyAgeDays { get; set; }

    [BsonElement("authorizedCapital")]
    public decimal? AuthorizedCapital { get; set; }
}

[BsonIgnoreExtraElements]
internal sealed class AuditNoteReadModel
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("riskAssessmentId")]
    public ObjectId RiskAssessmentId { get; set; }

    [BsonElement("author")]
    public AuditorReadModel? Author { get; set; }

    [BsonElement("authorId")]
    public string AuthorId { get; set; } = "";

    [BsonElement("authorName")]
    public string AuthorName { get; set; } = "";

    [BsonElement("text")]
    public string Text { get; set; } = "";

    [BsonElement("noteType")]
    public string NoteType { get; set; } = "";

    [BsonElement("isPinned")]
    public bool IsPinned { get; set; }

    [BsonElement("isResolved")]
    public bool IsResolved { get; set; }

    [BsonElement("discussionId")]
    public ObjectId? DiscussionId { get; set; }

    [BsonElement("smallAttachments")]
    public List<AttachmentReadModel> Attachments { get; set; } = [];

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
internal sealed class AttachmentReadModel
{
    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("url")]
    public string Url { get; set; } = "";
}
