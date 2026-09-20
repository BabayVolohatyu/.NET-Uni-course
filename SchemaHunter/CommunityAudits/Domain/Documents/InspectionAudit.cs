using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SchemaHunter.CommunityAudits.Domain.Documents;

/// <summary>
/// Primary aggregate — inspection_audits collection.
/// Embeds <see cref="AuditFinding"/> and <see cref="EvidenceAttachment"/> arrays.
/// </summary>
[BsonIgnoreExtraElements]
public class InspectionAudit
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>Human-readable audit identifier, e.g. "AUD-2024-UA-001". Unique.</summary>
    [BsonElement("auditCode")]
    public string AuditCode { get; set; } = null!;

    /// <summary>Cached reference to the Context 2 reconstruction project.</summary>
    [BsonElement("projectReference")]
    public ProjectReference ProjectReference { get; set; } = null!;

    /// <summary>Optional cached reference to the linked Context 1 tender.</summary>
    [BsonElement("tenderReference")]
    public TenderReference? TenderReference { get; set; }

    [BsonElement("auditorOrg")]
    public AuditorOrg AuditorOrg { get; set; } = null!;

    /// <summary>Draft | Submitted | UnderReview | Verified | Rejected</summary>
    [BsonElement("status")]
    public string Status { get; set; } = null!;

    /// <summary>None | Minor | Moderate | Severe | Critical</summary>
    [BsonElement("overallSeverity")]
    public string OverallSeverity { get; set; } = null!;

    [BsonElement("findings")]
    public List<AuditFinding> Findings { get; set; } = new();

    [BsonElement("evidenceAttachments")]
    public List<EvidenceAttachment> EvidenceAttachments { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
