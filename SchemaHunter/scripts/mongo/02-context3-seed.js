// =============================================================================
// Context 3 — Community Audits & Public Monitoring
// Seed Data — idempotent (updateOne + upsert:true on auditCode / topicTitle)
// Cross-context anchors:
//   • ROAD-UA-001 / UA-2024-01-15-000001-a  (Context 2 project / Context 1 tender)
//   • BLDG-UA-002 / UA-2024-02-20-000002-b
//   • BRDG-UA-003  (Context 2 only — no tender yet)
// =============================================================================
// Run via:  mongosh <connectionString> 02-context3-seed.js
// =============================================================================

const dbName = process.env.MONGO_AUDITS_DB || "SchemaHunterAudits";
db = db.getSiblingDB(dbName);

print("Seeding database: " + dbName);

// =============================================================================
// § inspection_audits (3 documents)
// =============================================================================

// ── Audit 1: ROAD-UA-001 — Verified, Critical severity ───────────────────────
// Demonstrates: tenderReference present (cross-context link), 2 findings (one resolved),
// 1 evidence attachment, status=Verified.
db.inspection_audits.updateOne(
    { auditCode: "AUD-2024-UA-001" },
    {
        $setOnInsert: {
            auditCode: "AUD-2024-UA-001",

            // Cached denormalised copy from Context 2 — no FK
            projectReference: {
                assetCode:   "ROAD-UA-001",
                projectName: "Reconstruction of M-07 Highway Segment Kyiv–Zhytomyr (km 12–27)",
                region:      "Kyiv Oblast"
            },

            // Cached reference from Context 1 — not all audits have this (see Audit 3)
            tenderReference: {
                prozorroId: "UA-2024-01-15-000001-a"
            },

            auditorOrg: {
                name:         "Transparency International Ukraine",
                edrpou:       "37222242",
                contactEmail: "audits@ti-ukraine.org"
            },

            status:          "Verified",
            overallSeverity: "Critical",

            // Embedded array — findings identified during inspection
            findings: [
                {
                    findingId:   "f1",
                    category:    "ContractCompliance",
                    description: "Subcontractor not listed in tender documentation. Work performed by unlicensed firm.",
                    severity:    "Critical",
                    isResolved:  false,
                    detectedAt:  new Date("2024-03-10T09:15:00Z")
                },
                {
                    findingId:   "f2",
                    category:    "MaterialQuality",
                    description: "Asphalt layer thickness 4 cm — below minimum specification of 6 cm (km 18–20).",
                    severity:    "Moderate",
                    isResolved:  true,
                    resolvedAt:  new Date("2024-03-20T11:00:00Z"),
                    detectedAt:  new Date("2024-03-10T10:00:00Z")
                }
            ],

            // Embedded array — photo / document evidence
            evidenceAttachments: [
                {
                    attachmentId: "att1",
                    url:          "https://storage.example.com/evidence/AUD-2024-UA-001/core-sample-km18.jpg",
                    fileHash:     "sha256:3a7b9c2d1e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b",
                    mimeType:     "image/jpeg",
                    sizeBytes:    2048000,
                    uploadedAt:   new Date("2024-03-10T09:30:00Z"),
                    uploadedBy:   "field.auditor@ti-ukraine.org"
                }
            ],

            createdAt: new Date("2024-03-10T08:00:00Z"),
            updatedAt: new Date("2024-03-22T14:00:00Z")
        }
    },
    { upsert: true }
);

// ── Audit 2: BLDG-UA-002 — UnderReview, Moderate severity ────────────────────
// Demonstrates: tenderReference present, 2 findings (both open),
// 2 evidence attachments, status=UnderReview.
db.inspection_audits.updateOne(
    { auditCode: "AUD-2024-UA-002" },
    {
        $setOnInsert: {
            auditCode: "AUD-2024-UA-002",

            projectReference: {
                assetCode:   "BLDG-UA-002",
                projectName: "Restoration of School No. 14, Kharkiv (Buildings A & B)",
                region:      "Kharkiv Oblast"
            },

            tenderReference: {
                prozorroId: "UA-2024-02-20-000002-b"
            },

            auditorOrg: {
                name:         "NGO Kharkiv Urban Watch",
                edrpou:       "43812905",
                contactEmail: "contact@kharkiv-watch.org.ua"
            },

            status:          "UnderReview",
            overallSeverity: "Moderate",

            findings: [
                {
                    findingId:   "f1",
                    category:    "SafetyViolation",
                    description: "Scaffolding on Building B does not meet DSTU EN 12811-1 safety standards.",
                    severity:    "Moderate",
                    isResolved:  false,
                    detectedAt:  new Date("2024-06-05T10:30:00Z")
                },
                {
                    findingId:   "f2",
                    category:    "Financial",
                    description: "Invoice for windows (invoice #INV-2024-087) exceeds market price by 38 %.",
                    severity:    "Moderate",
                    isResolved:  false,
                    detectedAt:  new Date("2024-06-05T14:00:00Z")
                }
            ],

            evidenceAttachments: [
                {
                    attachmentId: "att1",
                    url:          "https://storage.example.com/evidence/AUD-2024-UA-002/scaffold-photo.jpg",
                    fileHash:     "sha256:1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c",
                    mimeType:     "image/jpeg",
                    sizeBytes:    1740000,
                    uploadedAt:   new Date("2024-06-05T11:00:00Z"),
                    uploadedBy:   "inspector@kharkiv-watch.org.ua"
                },
                {
                    attachmentId: "att2",
                    url:          "https://storage.example.com/evidence/AUD-2024-UA-002/invoice-087-scan.pdf",
                    fileHash:     "sha256:9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a",
                    mimeType:     "application/pdf",
                    sizeBytes:    512000,
                    uploadedAt:   new Date("2024-06-05T15:30:00Z"),
                    uploadedBy:   "finance.analyst@kharkiv-watch.org.ua"
                }
            ],

            createdAt: new Date("2024-06-05T09:00:00Z"),
            updatedAt: new Date("2024-06-10T16:00:00Z")
        }
    },
    { upsert: true }
);

// ── Audit 3: BRDG-UA-003 — Draft, None severity ───────────────────────────────
// Demonstrates: NO tenderReference (bridge tender still active, none awarded)
//               → schema flexibility: same collection, different field set.
//               Empty findings and evidenceAttachments arrays.
db.inspection_audits.updateOne(
    { auditCode: "AUD-2024-UA-003" },
    {
        $setOnInsert: {
            auditCode: "AUD-2024-UA-003",

            projectReference: {
                assetCode:   "BRDG-UA-003",
                projectName: "Capital Repair of Bridge over Inhulets River on Route H-14",
                region:      "Kherson Oblast"
            },

            // tenderReference intentionally absent — demonstrates optional field / schema flexibility

            auditorOrg: {
                name:         "State Architecture and Construction Inspectorate",
                edrpou:       "37508547",
                contactEmail: "inspectorate@dabi.gov.ua"
            },

            status:          "Draft",
            overallSeverity: "None",

            findings:             [],
            evidenceAttachments:  [],

            // Extra field only on this document — demonstrates NoSQL schema flexibility
            plannedInspectionDate: new Date("2024-09-01T00:00:00Z"),
            internalNotes:         "Awaiting safe-access clearance before site visit.",

            createdAt: new Date("2024-07-20T12:00:00Z"),
            updatedAt: new Date("2024-07-20T12:00:00Z")
        }
    },
    { upsert: true }
);

print("  [OK] inspection_audits — 3 documents upserted");

// =============================================================================
// § discussion_threads (2 documents)
// Uses external reference (auditId → inspection_audits._id).
// Comments embed nested replies[] to demonstrate sub-document hierarchy.
// =============================================================================

const audit1Id = db.inspection_audits.findOne({ auditCode: "AUD-2024-UA-001" })._id;
const audit2Id = db.inspection_audits.findOne({ auditCode: "AUD-2024-UA-002" })._id;

// ── Thread 1: Road audit — active discussion ─────────────────────────────────
db.discussion_threads.updateOne(
    { auditId: audit1Id, "createdBy.userId": "user-0001" },
    {
        $setOnInsert: {
            auditId: audit1Id,

            topicTitle: "Unlicensed Subcontractor — Escalation Path?",

            createdBy: {
                userId:      "user-0001",
                displayName: "Olena Kovalenko",
                orgName:     "Transparency International Ukraine"
            },

            // Embedded comments with nested replies — demonstrates deep sub-document nesting
            comments: [
                {
                    commentId:  "c1",
                    authorName: "Vasyl Petrenko",
                    body:       "Has DABI been notified about the unlicensed subcontractor? Waiting on their response.",
                    postedAt:   new Date("2024-03-23T09:00:00Z"),
                    replies: [
                        {
                            replyId:    "r1",
                            authorName: "Olena Kovalenko",
                            body:       "Yes, notification sent on 22 March. Reference: DABI-2024-KY-1122.",
                            postedAt:   new Date("2024-03-23T10:15:00Z")
                        },
                        {
                            replyId:    "r2",
                            authorName: "Mykola Savchenko",
                            body:       "Please attach the official letter as evidence in the audit record.",
                            postedAt:   new Date("2024-03-23T11:30:00Z")
                        }
                    ]
                },
                {
                    commentId:  "c2",
                    authorName: "Iryna Bondarenko",
                    body:       "I cross-checked Prozorro — the winning bid is 99.7 % of the budget ceiling. " +
                                "Combined with the subcontractor issue, this looks like a coordinated scheme.",
                    postedAt:   new Date("2024-03-24T08:45:00Z"),
                    replies:    []
                }
            ],

            isLocked:  false,
            createdAt: new Date("2024-03-22T17:00:00Z"),
            updatedAt: new Date("2024-03-24T08:45:00Z")
        }
    },
    { upsert: true }
);

// ── Thread 2: School audit — locked after resolution ─────────────────────────
db.discussion_threads.updateOne(
    { auditId: audit2Id, "createdBy.userId": "user-0022" },
    {
        $setOnInsert: {
            auditId: audit2Id,

            topicTitle: "Overpriced Windows Invoice — What's the Resolution?",

            createdBy: {
                userId:      "user-0022",
                displayName: "Dmytro Lysenko",
                orgName:     "NGO Kharkiv Urban Watch"
            },

            comments: [
                {
                    commentId:  "c1",
                    authorName: "Natalia Honchar",
                    body:       "The invoice is now under NABU investigation — case number UA-NABU-2024-HRK-009.",
                    postedAt:   new Date("2024-06-12T13:00:00Z"),
                    replies: [
                        {
                            replyId:    "r1",
                            authorName: "Dmytro Lysenko",
                            body:       "Locking the thread until NABU investigation concludes per our moderation policy.",
                            postedAt:   new Date("2024-06-12T14:00:00Z")
                        }
                    ]
                }
            ],

            isLocked:  true,   // moderation — investigation in progress
            createdAt: new Date("2024-06-11T10:00:00Z"),
            updatedAt: new Date("2024-06-12T14:00:00Z")
        }
    },
    { upsert: true }
);

print("  [OK] discussion_threads — 2 documents upserted");

// =============================================================================
// § inspection_checklists (2 documents)
// Uses external reference (auditId → inspection_audits._id).
// dynamicFields varies by assetType — demonstrates schema-free NoSQL flexibility.
// =============================================================================

// ── Checklist 1: Road asset — Road-specific dynamic fields ───────────────────
db.inspection_checklists.updateOne(
    { auditId: audit1Id, assetType: "Road" },
    {
        $setOnInsert: {
            auditId: audit1Id,

            assetType:        "Road",
            checklistVersion: "v2.1",

            // Road-specific dynamic fields — not present in Building checklists
            dynamicFields: {
                surfaceConditionRating:     3,          // 1 (Critical) – 5 (Excellent)
                asphaltLayerThicknessCm:    4.1,        // measured vs required 6 cm
                baseLayerCondition:         "Acceptable",
                drainageSystemStatus:       "Blocked",
                roadMarkingCondition:       "Faded",
                guardrailsInstalled:        true,
                guardrailsLengthM:          840,
                complianceStatus:           "NonCompliant",
                nonComplianceReason:        "Asphalt layer below specification at km 18–20.",
                inspectionSpeedKmh:         40,
                weatherConditions:          "Overcast, 12°C"
            },

            completedBy: "field.auditor@ti-ukraine.org",
            completedAt: new Date("2024-03-10T16:00:00Z")
        }
    },
    { upsert: true }
);

// ── Checklist 2: Building asset — Building-specific dynamic fields ────────────
db.inspection_checklists.updateOne(
    { auditId: audit2Id, assetType: "Building" },
    {
        $setOnInsert: {
            auditId: audit2Id,

            assetType:        "Building",
            checklistVersion: "v1.4",

            // Building-specific dynamic fields — different schema from Road checklist
            dynamicFields: {
                structuralDamageGrade:     "B2",        // Ukrainian post-war damage classification
                roofReplacedPercent:       100,
                windowsReplaced:           true,
                windowsReplacedCount:      148,
                electricalRewired:         false,
                plumbingRestored:          true,
                accessibilityCompliant:    false,       // not yet wheelchair accessible
                fireAlarmInstalled:        true,
                complianceStatus:          "PartiallyCompliant",
                nonComplianceReason:       "Accessibility ramp not yet installed; scaffolding safety issues.",
                occupancyPermitIssued:     false,
                floorAreaReconstructedSqm: 2800
            },

            completedBy: "inspector@kharkiv-watch.org.ua",
            completedAt: new Date("2024-06-05T17:00:00Z")
        }
    },
    { upsert: true }
);

print("  [OK] inspection_checklists — 2 documents upserted");
print("Context 3 seed data inserted successfully.");
