// =============================================================================
// Context 3 — Community Audits & Public Monitoring
// MongoDB Index Creation
// Source: context3-community-audits-mongo-guide.md  §2.2, §3.2, §4.3
// =============================================================================
// Run via:  mongosh <connectionString> 01-context3-indexes.js
//
// The MONGO_AUDITS_DB env var is injected by the init container.
// When running locally, set the database explicitly or pass the URI with the DB.
// =============================================================================

// ── Switch to the audits database ─────────────────────────────────────────────
const dbName = process.env.MONGO_AUDITS_DB || "SchemaHunterAudits";
db = db.getSiblingDB(dbName);

print("Creating indexes in database: " + dbName);

// =============================================================================
// §2.2  Collection: inspection_audits
// =============================================================================

// Unique index on auditCode — human-readable identifier
db.inspection_audits.createIndex(
    { auditCode: 1 },
    { unique: true, name: "ix_inspection_audits_audit_code_unique" }
);

// Filter by status + region — dashboard queries
db.inspection_audits.createIndex(
    { status: 1, "projectReference.region": 1 },
    { name: "ix_inspection_audits_status_region" }
);

// Cross-context lookup by project asset code
db.inspection_audits.createIndex(
    { "projectReference.assetCode": 1 },
    { name: "ix_inspection_audits_asset_code" }
);

// Cross-context lookup by Prozorro tender ID (sparse — not all audits link to a tender)
db.inspection_audits.createIndex(
    { "tenderReference.prozorroId": 1 },
    { sparse: true, name: "ix_inspection_audits_prozorro_id" }
);

// Severity filter + sort by date — leaderboard / monitoring dashboard
db.inspection_audits.createIndex(
    { overallSeverity: 1, createdAt: -1 },
    { name: "ix_inspection_audits_severity_date" }
);

print("  [OK] inspection_audits — 5 indexes created");

// =============================================================================
// §3.2  Collection: discussion_threads
// =============================================================================

// Primary lookup — all threads for a given audit
db.discussion_threads.createIndex(
    { auditId: 1, createdAt: -1 },
    { name: "ix_discussion_threads_audit_id_date" }
);

// Locked status filter — moderation queries
db.discussion_threads.createIndex(
    { isLocked: 1 },
    { name: "ix_discussion_threads_is_locked" }
);

print("  [OK] discussion_threads — 2 indexes created");

// =============================================================================
// §4.3  Collection: inspection_checklists
// =============================================================================

// Primary lookup — checklist(s) for a given audit
db.inspection_checklists.createIndex(
    { auditId: 1 },
    { name: "ix_inspection_checklists_audit_id" }
);

// Query by asset type — compare checklists across same type
db.inspection_checklists.createIndex(
    { assetType: 1, completedAt: -1 },
    { name: "ix_inspection_checklists_asset_type_date" }
);

// Filter by dynamic compliance fields — Road-specific, sparse for other asset types
db.inspection_checklists.createIndex(
    { "dynamicFields.complianceStatus": 1 },
    { sparse: true, name: "ix_inspection_checklists_compliance_status" }
);

print("  [OK] inspection_checklists — 3 indexes created");
print("Context 3 indexes created successfully.");
