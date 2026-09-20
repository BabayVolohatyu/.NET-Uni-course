using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SchemaHunter.CommunityAudits.Domain.Documents;

/// <summary>
/// Tertiary aggregate — inspection_checklists collection.
/// Dynamic fields are stored in a <see cref="BsonDocument"/> to support
/// per-asset-type schemas without a fixed C# model.
/// </summary>
[BsonIgnoreExtraElements]
public class InspectionChecklist
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>External reference to <c>inspection_audits._id</c>.</summary>
    [BsonElement("auditId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AuditId { get; set; } = null!;

    /// <summary>Road | Building | Bridge | EnergyInfrastructure | WaterSupply</summary>
    [BsonElement("assetType")]
    public string AssetType { get; set; } = null!;

    [BsonElement("checklistVersion")]
    public string ChecklistVersion { get; set; } = null!;

    /// <summary>
    /// Fully dynamic schema-free fields — content varies by <see cref="AssetType"/>.
    /// Example Road keys: asphaltLayerThicknessCm, complianceStatus, roadMarkingCondition, etc.
    /// </summary>
    [BsonElement("dynamicFields")]
    public BsonDocument DynamicFields { get; set; } = new();

    [BsonElement("completedBy")]
    public string CompletedBy { get; set; } = null!;

    [BsonElement("completedAt")]
    public DateTime CompletedAt { get; set; }
}
