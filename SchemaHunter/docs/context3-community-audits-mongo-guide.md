# Context 3 — Community Audits & Public Monitoring
## MongoDB Implementation Guide

**Bounded Context:** Community Audits & Public Monitoring  
**Persistence:** MongoDB — document model  
**Isolation rule:** No cross-database FKs. References to Context 1 tenders and Context 2 projects are stored as **embedded cached subdocuments** (`tenderReference`, `projectReference`) inside each audit document.

---

## 1. Domain Overview

```
inspection_audits          (primary aggregate)
  ├── findings[]           embedded array
  └── evidenceAttachments[] embedded array

discussion_threads         (secondary aggregate)
  └── auditId: ObjectId    → references inspection_audits._id
      └── comments[]       embedded with nested replies[]

inspection_checklists      (tertiary aggregate)
  └── auditId: ObjectId    → references inspection_audits._id
      └── dynamicFields{}  schema varies by assetType
```

**Embedding vs referencing decision:**
- `findings` and `evidenceAttachments` are **embedded** — they are never queried independently, always loaded with the audit, and bounded in size (< 100 items).
- `DiscussionThread` and `InspectionChecklist` are **separate collections** (external reference pattern) — they grow independently, can be large, and must be queried without loading the full audit.

---

## 2. Collection: `inspection_audits`

### 2.1 Document Schema

```json
{
  "_id": "ObjectId('665a1b2c3d4e5f6789abcdef')",

  "auditCode": "AUD-2024-UA-001",

  // Cached denormalized copy from Context 2 — no FK
  "projectReference": {
    "assetCode":   "ROAD-UA-001",
    "projectName": "Reconstruction of M-07 Highway Segment Kyiv–Zhytomyr",
    "region":      "Kyiv Oblast"
  },

  // Cached denormalized copy from Context 1 — nullable, not all audits link to a tender
  "tenderReference": {
    "prozorroId": "UA-2024-01-15-000001-a"
  },

  "auditorOrg": {
    "name":         "Transparency International Ukraine",
    "edrpou":       "37222242",
    "contactEmail": "audits@ti-ukraine.org"
  },

  // Draft | Submitted | UnderReview | Verified | Rejected
  "status": "Verified",

  // None | Minor | Moderate | Severe | Critical
  "overallSeverity": "Severe",

  // Embedded array — violation findings identified during the inspection
  "findings": [
    {
      "findingId":   "f1",
      "category":    "ContractCompliance",
      "description": "Subcontractor not listed in tender documentation.",
      "severity":    "Severe",
      "isResolved":  false,
      "detectedAt":  "2024-03-10T09:15:00Z"
    },
    {
      "findingId":   "f2",
      "category":    "MaterialQuality",
      "description": "Asphalt layer thickness below specification (4 cm vs required 6 cm).",
      "severity":    "Moderate",
      "isResolved":  true,
      "resolvedAt":  "2024-03-20T11:00:00Z",
      "detectedAt":  "2024-03-10T10:00:00Z"
    }
  ],

  // Embedded array — photo / document evidence
  "evidenceAttachments": [
    {
      "attachmentId": "att1",
      "url":          "https://storage.example.com/evidence/AUD-2024-UA-001/photo1.jpg",
      "fileHash":     "sha256:abc123...",
      "mimeType":     "image/jpeg",
      "sizeBytes":    2048000,
      "uploadedAt":   "2024-03-10T09:30:00Z",
      "uploadedBy":   "auditor@ti-ukraine.org"
    }
  ],

  "createdAt":  "2024-03-10T08:00:00Z",
  "updatedAt":  "2024-03-20T11:05:00Z"
}
```

### 2.2 Index Creation

```javascript
// Unique index on auditCode — human-readable identifier
db.inspection_audits.createIndex(
  { auditCode: 1 },
  { unique: true, name: "ix_inspection_audits_audit_code_unique" }
);

// Filter by status — dashboard queries
db.inspection_audits.createIndex(
  { status: 1, "projectReference.region": 1 },
  { name: "ix_inspection_audits_status_region" }
);

// Cross-context lookup by project asset code
db.inspection_audits.createIndex(
  { "projectReference.assetCode": 1 },
  { name: "ix_inspection_audits_asset_code" }
);

// Cross-context lookup by Prozorro tender ID
db.inspection_audits.createIndex(
  { "tenderReference.prozorroId": 1 },
  { sparse: true, name: "ix_inspection_audits_prozorro_id" }
);

// Severity filter + sort by date
db.inspection_audits.createIndex(
  { overallSeverity: 1, createdAt: -1 },
  { name: "ix_inspection_audits_severity_date" }
);
```

---

## 3. Collection: `discussion_threads`

### 3.1 Document Schema

```json
{
  "_id": "ObjectId('665b2c3d4e5f6789abcdef01')",

  // External reference to inspection_audits._id
  "auditId": "ObjectId('665a1b2c3d4e5f6789abcdef')",

  "topicTitle": "Question about subcontractor disclosure obligation",

  "createdBy": {
    "userId":      "usr_9988776655",
    "displayName": "Olena Kovalenko",
    "orgName":     "Transparency International Ukraine"
  },

  // Nested embedded array with threaded replies
  "comments": [
    {
      "commentId":  "c1",
      "authorName": "Olena Kovalenko",
      "body":       "The contract required all subcontractors to be listed upfront. This is a clear violation.",
      "postedAt":   "2024-03-11T10:00:00Z",
      "replies": [
        {
          "replyId":    "r1",
          "authorName": "Mykola Sydorenko",
          "body":       "Agreed. The legal basis is Art. 41 of the Public Procurement Law.",
          "postedAt":   "2024-03-11T11:30:00Z"
        },
        {
          "replyId":    "r2",
          "authorName": "НАЗК Monitor Bot",
          "body":       "Flagged for NAZK review automatically.",
          "postedAt":   "2024-03-11T12:00:00Z"
        }
      ]
    },
    {
      "commentId":  "c2",
      "authorName": "Dmytro Petrenko",
      "body":       "Can the procuring entity respond officially here?",
      "postedAt":   "2024-03-12T09:00:00Z",
      "replies": []
    }
  ],

  "isLocked":  false,
  "createdAt": "2024-03-11T09:45:00Z",
  "updatedAt": "2024-03-12T09:00:00Z"
}
```

### 3.2 Index Creation

```javascript
// Primary lookup — all threads for a given audit
db.discussion_threads.createIndex(
  { auditId: 1, createdAt: -1 },
  { name: "ix_discussion_threads_audit_id_date" }
);

// Locked status filter
db.discussion_threads.createIndex(
  { isLocked: 1 },
  { name: "ix_discussion_threads_is_locked" }
);
```

---

## 4. Collection: `inspection_checklists`

### 4.1 Document Schema — Road Asset

```json
{
  "_id": "ObjectId('665c3d4e5f6789abcdef0123')",

  // External reference to inspection_audits._id
  "auditId": "ObjectId('665a1b2c3d4e5f6789abcdef')",

  // Road | Building | Bridge | EnergyInfrastructure | WaterSupply
  "assetType": "Road",

  "checklistVersion": "v2.1",

  // Dynamic fields — schema varies by assetType
  // Road-specific parameters:
  "dynamicFields": {
    "asphaltLayerThicknessCm":   4.2,
    "requiredThicknessCm":       6.0,
    "complianceStatus":          "NonCompliant",
    "roadMarkingCondition":      "Faded",
    "drainageSystemFunctional":  true,
    "guardrailsInstalled":       false,
    "signageCompliant":          true,
    "surfaceRoughnessIRI":       3.8,
    "loadCapacityTonnes":        40,
    "lastInspectionDate":        "2024-03-10"
  },

  "completedBy":  "auditor@ti-ukraine.org",
  "completedAt":  "2024-03-10T14:00:00Z"
}
```

### 4.2 Document Schema — Building Asset

```json
{
  "_id": "ObjectId('665c3d4e5f6789abcdef0124')",
  "auditId": "ObjectId('665a2b3c4d5e6f7890abcde1')",
  "assetType": "Building",
  "checklistVersion": "v1.3",
  "dynamicFields": {
    "structuralIntegrityScore":  87,
    "windowsReplaced":           true,
    "roofWaterproofingInstalled": true,
    "solarPanelsInstalled":      false,
    "solarPanelCapacityKWh":     null,
    "heatingSystemType":         "HeatPump",
    "energyEfficiencyClass":     "B",
    "accessibilityRampInstalled": true,
    "sprinklerSystemFunctional": false,
    "lastFireSafetyInspection":  "2023-11-01"
  },
  "completedBy": "inspector@rebuild-ua.org",
  "completedAt": "2024-04-05T10:30:00Z"
}
```

### 4.3 Index Creation

```javascript
// Primary lookup — checklist for a given audit
db.inspection_checklists.createIndex(
  { auditId: 1 },
  { name: "ix_inspection_checklists_audit_id" }
);

// Query by asset type — compare checklists across same type
db.inspection_checklists.createIndex(
  { assetType: 1, completedAt: -1 },
  { name: "ix_inspection_checklists_asset_type_date" }
);

// Filter by dynamic compliance fields (Road)
db.inspection_checklists.createIndex(
  { "dynamicFields.complianceStatus": 1 },
  { sparse: true, name: "ix_inspection_checklists_compliance_status" }
);
```

---

## 5. Aggregation Pipelines

### 5.1 Severity Distribution by Region

Groups all verified/submitted audits and counts severity levels per region.

```javascript
db.inspection_audits.aggregate([
  // Stage 1: only active audits
  {
    $match: {
      status: { $in: ["Submitted", "UnderReview", "Verified"] }
    }
  },
  // Stage 2: group by region + severity
  {
    $group: {
      _id: {
        region:   "$projectReference.region",
        severity: "$overallSeverity"
      },
      count: { $sum: 1 }
    }
  },
  // Stage 3: reshape into region-level documents
  {
    $group: {
      _id: "$_id.region",
      severities: {
        $push: {
          severity: "$_id.severity",
          count:    "$count"
        }
      },
      totalAudits: { $sum: "$count" }
    }
  },
  // Stage 4: sort by total descending
  { $sort: { totalAudits: -1 } },
  // Stage 5: project clean output
  {
    $project: {
      _id:         0,
      region:      "$_id",
      totalAudits: 1,
      severities:  1
    }
  }
]);
```

**Sample output:**
```json
[
  {
    "region": "Kharkiv Oblast",
    "totalAudits": 42,
    "severities": [
      { "severity": "Critical", "count": 8 },
      { "severity": "Severe",   "count": 14 }
    ]
  }
]
```

---

### 5.2 Top Auditor Organisations by Unresolved Findings

Unwinds the embedded `findings` array and aggregates unresolved items per organisation.

```javascript
db.inspection_audits.aggregate([
  // Stage 1: unwind findings array
  { $unwind: "$findings" },
  // Stage 2: keep only unresolved findings
  {
    $match: {
      "findings.isResolved": false
    }
  },
  // Stage 3: group by auditor organisation
  {
    $group: {
      _id: {
        orgName: "$auditorOrg.name",
        edrpou:  "$auditorOrg.edrpou"
      },
      unresolvedCount:      { $sum: 1 },
      criticalCount:        {
        $sum: {
          $cond: [{ $eq: ["$findings.severity", "Critical"] }, 1, 0]
        }
      },
      affectedAuditCount:   { $addToSet: "$_id" }
    }
  },
  // Stage 4: add array size as scalar
  {
    $addFields: {
      affectedAuditCount: { $size: "$affectedAuditCount" }
    }
  },
  // Stage 5: sort by unresolved findings descending
  { $sort: { unresolvedCount: -1 } },
  // Stage 6: top 10 organisations
  { $limit: 10 },
  // Stage 7: clean output
  {
    $project: {
      _id:                0,
      orgName:            "$_id.orgName",
      edrpou:             "$_id.edrpou",
      unresolvedCount:    1,
      criticalCount:      1,
      affectedAuditCount: 1
    }
  }
]);
```

---

### 5.3 Evidence Upload Trend by Month

Counts evidence attachments uploaded per calendar month — useful for monitoring NGO activity over time.

```javascript
db.inspection_audits.aggregate([
  // Stage 1: unwind evidenceAttachments
  { $unwind: "$evidenceAttachments" },
  // Stage 2: extract year-month from uploadedAt
  {
    $addFields: {
      uploadMonth: {
        $dateToString: {
          format: "%Y-%m",
          date:   { $toDate: "$evidenceAttachments.uploadedAt" }
        }
      }
    }
  },
  // Stage 3: group by month
  {
    $group: {
      _id:             "$uploadMonth",
      totalUploads:    { $sum: 1 },
      totalSizeBytes:  { $sum: "$evidenceAttachments.sizeBytes" },
      uniqueAudits:    { $addToSet: "$_id" }
    }
  },
  // Stage 4: compute unique audit count
  {
    $addFields: {
      uniqueAuditCount: { $size: "$uniqueAudits" }
    }
  },
  // Stage 5: sort chronologically
  { $sort: { _id: 1 } },
  // Stage 6: project clean output
  {
    $project: {
      _id:              0,
      month:            "$_id",
      totalUploads:     1,
      totalSizeBytes:   1,
      uniqueAuditCount: 1
    }
  }
]);
```

---

## 6. C# MongoDB Driver — POCO Mappings

### 6.1 Project Setup

```xml
<PackageReference Include="MongoDB.Driver" Version="3.*" />
```

### 6.2 Subdocument POCOs

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class ProjectReference
{
    [BsonElement("assetCode")]
    public string AssetCode { get; set; } = null!;

    [BsonElement("projectName")]
    public string ProjectName { get; set; } = null!;

    [BsonElement("region")]
    public string Region { get; set; } = null!;
}

public class TenderReference
{
    [BsonElement("prozorroId")]
    public string ProzorroId { get; set; } = null!;
}

public class AuditorOrg
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("edrpou")]
    public string Edrpou { get; set; } = null!;

    [BsonElement("contactEmail")]
    public string ContactEmail { get; set; } = null!;
}

public class AuditFinding
{
    [BsonElement("findingId")]
    public string FindingId { get; set; } = null!;

    [BsonElement("category")]
    public string Category { get; set; } = null!;

    [BsonElement("description")]
    public string Description { get; set; } = null!;

    [BsonElement("severity")]
    public string Severity { get; set; } = null!;

    [BsonElement("isResolved")]
    public bool IsResolved { get; set; }

    [BsonElement("detectedAt")]
    public DateTime DetectedAt { get; set; }

    [BsonElement("resolvedAt")]
    public DateTime? ResolvedAt { get; set; }
}

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
```

### 6.3 `InspectionAudit` Document

```csharp
[BsonCollection("inspection_audits")]
public class InspectionAudit
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("auditCode")]
    public string AuditCode { get; set; } = null!;

    [BsonElement("projectReference")]
    public ProjectReference ProjectReference { get; set; } = null!;

    [BsonElement("tenderReference")]
    public TenderReference? TenderReference { get; set; }

    [BsonElement("auditorOrg")]
    public AuditorOrg AuditorOrg { get; set; } = null!;

    [BsonElement("status")]
    public string Status { get; set; } = null!;

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
```

### 6.4 `DiscussionThread` Document

```csharp
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

public class DiscussionThread
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

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

public class ThreadCreator
{
    [BsonElement("userId")]
    public string UserId { get; set; } = null!;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = null!;

    [BsonElement("orgName")]
    public string OrgName { get; set; } = null!;
}
```

### 6.5 `InspectionChecklist` Document (dynamic fields via `BsonDocument`)

```csharp
public class InspectionChecklist
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("auditId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AuditId { get; set; } = null!;

    [BsonElement("assetType")]
    public string AssetType { get; set; } = null!;

    [BsonElement("checklistVersion")]
    public string ChecklistVersion { get; set; } = null!;

    // BsonDocument allows fully dynamic, schema-free field storage per asset type
    [BsonElement("dynamicFields")]
    public BsonDocument DynamicFields { get; set; } = new();

    [BsonElement("completedBy")]
    public string CompletedBy { get; set; } = null!;

    [BsonElement("completedAt")]
    public DateTime CompletedAt { get; set; }
}
```

### 6.6 Repository Example — Querying and Inserting

```csharp
using MongoDB.Driver;

public class InspectionAuditRepository
{
    private readonly IMongoCollection<InspectionAudit> _collection;

    public InspectionAuditRepository(IMongoDatabase database)
        => _collection = database.GetCollection<InspectionAudit>("inspection_audits");

    public async Task<InspectionAudit?> GetByAuditCodeAsync(string auditCode, CancellationToken ct = default)
        => await _collection
            .Find(a => a.AuditCode == auditCode)
            .FirstOrDefaultAsync(ct);

    public async Task<List<InspectionAudit>> GetByProjectAsync(string assetCode, CancellationToken ct = default)
        => await _collection
            .Find(a => a.ProjectReference.AssetCode == assetCode)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task InsertAsync(InspectionAudit audit, CancellationToken ct = default)
        => await _collection.InsertOneAsync(audit, cancellationToken: ct);

    public async Task MarkFindingResolvedAsync(
        string auditId, string findingId, CancellationToken ct = default)
    {
        var filter = Builders<InspectionAudit>.Filter.And(
            Builders<InspectionAudit>.Filter.Eq(a => a.Id, auditId),
            Builders<InspectionAudit>.Filter.ElemMatch(
                a => a.Findings, f => f.FindingId == findingId));

        var update = Builders<InspectionAudit>.Update
            .Set("findings.$.isResolved", true)
            .Set("findings.$.resolvedAt", DateTime.UtcNow)
            .Set(a => a.UpdatedAt,        DateTime.UtcNow);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
```

---

## 7. Eventual Consistency — Event Contracts

### 7.1 Project renamed in Context 2

```json
{
  "eventType": "ProjectAssetCodeRenamed",
  "payload": { "oldAssetCode": "ROAD-UA-001", "newAssetCode": "ROAD-UA-001-REV", "newName": "..." }
}
```

MongoDB subscriber executes:

```csharp
var filter = Builders<InspectionAudit>.Filter
    .Eq("projectReference.assetCode", oldAssetCode);

var update = Builders<InspectionAudit>.Update
    .Set("projectReference.assetCode", newAssetCode)
    .Set("projectReference.projectName", newName)
    .Set(a => a.UpdatedAt, DateTime.UtcNow);

await _collection.UpdateManyAsync(filter, update);
```

### 7.2 Tender status changed in Context 1

```json
{
  "eventType": "TenderStatusChanged",
  "payload": { "prozorroId": "UA-2024-01-15-000001-a", "newStatus": "Awarded" }
}
```

No update needed in the audit documents — `tenderReference` only caches the identifier, not the mutable status. Queries that need live tender status must call Context 1's API.

---

## 8. Index Strategy Summary

| Collection | Index | Purpose |
|---|---|---|
| `inspection_audits` | `auditCode` unique | Human-readable lookup |
| `inspection_audits` | `status + region` | Dashboard filter |
| `inspection_audits` | `projectReference.assetCode` | Cross-context link |
| `inspection_audits` | `tenderReference.prozorroId` sparse | Cross-context link |
| `inspection_audits` | `overallSeverity + createdAt` | Severity leaderboard |
| `discussion_threads` | `auditId + createdAt` | Thread list per audit |
| `discussion_threads` | `isLocked` | Moderation filter |
| `inspection_checklists` | `auditId` | Checklist per audit |
| `inspection_checklists` | `assetType + completedAt` | Type-based analytics |
| `inspection_checklists` | `dynamicFields.complianceStatus` sparse | Road compliance filter |
