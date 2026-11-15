// ============================================================
// Project №3: MongoDB Collection Definitions & Indexes
// Context: Risk Assessment & Anomaly Detection
// Domain: Automated Anomaly Detection in Public Reconstruction Procurement
// ============================================================
// Run order: collections.js → seed.js
// Execute with: mongosh mongodb://localhost:27017 collections.js
// ============================================================

// Switch to / create the database
const db = connect("mongodb://localhost:27017/RiskAssessment");

// ============================================================
// COLLECTION 1: riskAssessments
// ============================================================
// Per-tender risk assessment result.
// Modeling approaches used:
//   - EMBEDDING: triggeredFlags[] array — flags are always loaded with the assessment (tight coupling)
//   - REFERENCING: auditNoteIds[] — audit notes can be large; referenced to avoid document bloat
//   - DENORMALIZATION: tenderTitle, procuringEntityName — snapshots from P1 (Bidding context)
//     to avoid cross-service calls on every read (duplication policy: snapshot, not updated)
// ============================================================
db.createCollection("riskAssessments", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["tenderId", "tenderTitle", "procuringEntityId", "overallRiskScore", "riskLevel", "createdAt"],
      properties: {
        tenderId: {
          bsonType: "long",
          description: "ID of the tender in ProcurementBidding DB (plain reference, no FK)"
        },
        tenderTitle: {
          bsonType: "string",
          description: "SNAPSHOT from Bidding context — fixed at assessment creation time, not updated"
        },
        procuringEntityId: { bsonType: "long" },
        procuringEntityName: {
          bsonType: "string",
          description: "SNAPSHOT from Bidding context — denormalized for read optimization"
        },
        overallRiskScore: {
          bsonType: "double",
          minimum: 0,
          maximum: 100,
          description: "Composite risk score 0–100"
        },
        riskLevel: {
          bsonType: "string",
          enum: ["Low", "Medium", "High", "Critical"]
        },
        triggeredFlags: {
          bsonType: "array",
          description: "EMBEDDED subdocuments — always read together with the assessment",
          items: {
            bsonType: "object",
            required: ["ruleCode", "severity", "description"],
            properties: {
              ruleCode:    { bsonType: "string" },
              severity:    { bsonType: "string", enum: ["Info", "Warning", "Critical"] },
              description: { bsonType: "string" },
              detectedAt:  { bsonType: "date" },
              affectedField: { bsonType: "string" }
            }
          }
        },
        auditNoteIds: {
          bsonType: "array",
          description: "REFERENCES to auditNotes collection — notes can be large; kept separate",
          items: { bsonType: "objectId" }
        },
        assessedAt:  { bsonType: "date" },
        assessedBy:  { bsonType: "string" },
        createdAt:   { bsonType: "date" },
        updatedAt:   { bsonType: "date" }
      }
    }
  }
});

// Indexes for riskAssessments
// "Find all assessments for a specific tender" — primary lookup query
db.riskAssessments.createIndex({ tenderId: 1 }, { unique: true, name: "IX_riskAssessments_tenderId" });

// "Dashboard: list critical/high tenders sorted by score desc" — auditor dashboard query
db.riskAssessments.createIndex(
  { riskLevel: 1, overallRiskScore: -1 },
  { name: "IX_riskAssessments_riskLevel_score" }
);

// "Filter assessments by procuring entity" — entity-level risk profiling query
db.riskAssessments.createIndex(
  { procuringEntityId: 1, createdAt: -1 },
  { name: "IX_riskAssessments_entity_date" }
);

// "Text search over tender titles" — search bar query
db.riskAssessments.createIndex(
  { tenderTitle: "text" },
  { name: "IX_riskAssessments_tenderTitle_text" }
);

print("Collection 'riskAssessments' created with schema validation and indexes.");

// ============================================================
// COLLECTION 2: anomalyRules
// ============================================================
// Catalog of detection rules / heuristics applied to tenders.
// FLEXIBLE SCHEMA is demonstrated here: different rule types have
// different parameter shapes in the same collection.
// - SingleBidderRule has: minBidderThreshold
// - PriceDumpingRule has: maxPriceRatioPct
// - ShellCompanyRule has: maxCompanyAgeDays, maxAuthorizedCapital
// No embedding or referencing needed — this is a configuration catalog.
// ============================================================
db.createCollection("anomalyRules");

db.anomalyRules.createIndex({ code: 1 }, { unique: true, name: "IX_anomalyRules_code" });
db.anomalyRules.createIndex({ isActive: 1, severity: 1 }, { name: "IX_anomalyRules_active_severity" });

print("Collection 'anomalyRules' created.");

// ============================================================
// COLLECTION 3: auditNotes
// ============================================================
// Auditor-written notes and annotations linked to a risk assessment.
// Modeling approaches:
//   - EMBEDDING: smallAttachments[] — small inline attachments (links, file names)
//   - REFERENCING: discussionId — long threaded discussions kept in a separate collection
//     to avoid exceeding the 16 MB document limit and simplify partial updates
//   - HYBRID: short notes embed everything; long notes reference a discussion thread
// ============================================================
db.createCollection("auditNotes", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["riskAssessmentId", "authorId", "authorName", "text", "createdAt"],
      properties: {
        riskAssessmentId: { bsonType: "objectId" },
        authorId:   { bsonType: "string" },
        authorName: { bsonType: "string" },
        text:       { bsonType: "string", maxLength: 5000 },
        noteType: {
          bsonType: "string",
          enum: ["Observation", "Request", "Decision", "Escalation"]
        },
        // EMBEDDED: small inline attachments (URL or filename — always read with note)
        smallAttachments: {
          bsonType: "array",
          items: {
            bsonType: "object",
            properties: {
              name: { bsonType: "string" },
              url:  { bsonType: "string" }
            }
          }
        },
        // REFERENCE: id of a large discussion thread in the discussions collection
        discussionId: { bsonType: "objectId" },
        isPinned:  { bsonType: "bool" },
        isResolved: { bsonType: "bool" },
        createdAt:  { bsonType: "date" },
        updatedAt:  { bsonType: "date" }
      }
    }
  }
});

// "Retrieve all notes for an assessment in creation order" — audit trail query
db.auditNotes.createIndex(
  { riskAssessmentId: 1, createdAt: 1 },
  { name: "IX_auditNotes_assessmentId_date" }
);

// "Find unresolved / pinned notes for triage" — daily auditor workflow query
db.auditNotes.createIndex(
  { isResolved: 1, isPinned: -1 },
  { name: "IX_auditNotes_unresolved_pinned" }
);

print("Collection 'auditNotes' created with schema validation and indexes.");

// ============================================================
// COLLECTION 4: discussions
// ============================================================
// Threaded discussion associated with an audit note.
// REFERENCED from auditNotes.discussionId — not embedded because
// discussions can grow to thousands of messages.
// Messages array is embedded because all messages belong to this discussion
// and are always read together (up to pagination).
// ============================================================
db.createCollection("discussions");

// "Find discussion by its _id (from auditNotes reference)" — standard reference lookup
db.discussions.createIndex({ auditNoteId: 1 }, { name: "IX_discussions_auditNoteId" });

print("Collection 'discussions' created.");
print("All collections created successfully.");
