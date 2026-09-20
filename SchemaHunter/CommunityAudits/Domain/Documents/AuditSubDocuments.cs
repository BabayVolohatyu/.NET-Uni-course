using MongoDB.Bson.Serialization.Attributes;

namespace SchemaHunter.CommunityAudits.Domain.Documents;

/// <summary>Cached, denormalised reference to a Context 2 reconstruction project.</summary>
public class ProjectReference
{
    [BsonElement("assetCode")]
    public string AssetCode { get; set; } = null!;

    [BsonElement("projectName")]
    public string ProjectName { get; set; } = null!;

    [BsonElement("region")]
    public string Region { get; set; } = null!;
}

/// <summary>Cached, denormalised reference to a Context 1 tender (Prozorro ID only).</summary>
public class TenderReference
{
    [BsonElement("prozorroId")]
    public string ProzorroId { get; set; } = null!;
}

/// <summary>Details of the organisation that performed the audit.</summary>
public class AuditorOrg
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("edrpou")]
    public string Edrpou { get; set; } = null!;

    [BsonElement("contactEmail")]
    public string ContactEmail { get; set; } = null!;
}

/// <summary>A single violation finding embedded inside an <see cref="InspectionAudit"/>.</summary>
public class AuditFinding
{
    [BsonElement("findingId")]
    public string FindingId { get; set; } = null!;

    /// <summary>ContractCompliance | MaterialQuality | SafetyViolation | Financial | Other</summary>
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    [BsonElement("description")]
    public string Description { get; set; } = null!;

    /// <summary>None | Minor | Moderate | Severe | Critical</summary>
    [BsonElement("severity")]
    public string Severity { get; set; } = null!;

    [BsonElement("isResolved")]
    public bool IsResolved { get; set; }

    [BsonElement("detectedAt")]
    public DateTime DetectedAt { get; set; }

    [BsonElement("resolvedAt")]
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>A photo or document attachment embedded inside an <see cref="InspectionAudit"/>.</summary>
public class EvidenceAttachment
{
    [BsonElement("attachmentId")]
    public string AttachmentId { get; set; } = null!;

    [BsonElement("url")]
    public string Url { get; set; } = null!;

    [BsonElement("fileHash")]
    public string FileHash { get; set; } = null!;

    [BsonElement("mimeType")]
    public string MimeType { get; set; } = null!;

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }

    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    [BsonElement("uploadedBy")]
    public string UploadedBy { get; set; } = null!;
}
