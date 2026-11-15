// ============================================================
// Project №3: Seed Data — Risk Assessment Context (MongoDB)
// Run order: collections.js → seed.js
// Execute with: mongosh mongodb://localhost:27017 seed.js
// IDEMPOTENT: uses updateOne with upsert:true — safe to run multiple times.
// ============================================================

const db = connect("mongodb://localhost:27017/RiskAssessment");

// ============================================================
// Seed: anomalyRules
// FLEXIBLE SCHEMA DEMONSTRATION: different rule types have
// different parameter shapes within the same collection.
// ============================================================

// Rule 1: Single-bidder — no extra numeric parameters
db.anomalyRules.updateOne(
  { code: "SINGLE_BIDDER" },
  {
    $setOnInsert: {
      code:        "SINGLE_BIDDER",
      name:        "Єдиний учасник",
      severity:    "Warning",
      description: "Тендер отримав лише одну пропозицію, що може свідчити про штучне обмеження конкуренції.",
      isActive:    true,
      // This rule type has no numeric thresholds — demonstrates flexible schema
      createdAt:   new Date()
    }
  },
  { upsert: true }
);

// Rule 2: Price dumping — has maxPriceRatioPct (not present in Rule 1)
db.anomalyRules.updateOne(
  { code: "PRICE_DUMPING" },
  {
    $setOnInsert: {
      code:              "PRICE_DUMPING",
      name:              "Демпінгова ціна",
      severity:          "Critical",
      description:       "Запропонована ціна менша за порогове значення відносно очікуваної вартості.",
      isActive:          true,
      maxPriceRatioPct:  50,   // own parameter — absent in SINGLE_BIDDER and SHELL_COMPANY rules
      createdAt:         new Date()
    }
  },
  { upsert: true }
);

// Rule 3: Shell company — has maxCompanyAgeDays and maxAuthorizedCapital (unique shape)
db.anomalyRules.updateOne(
  { code: "SHELL_COMPANY" },
  {
    $setOnInsert: {
      code:                 "SHELL_COMPANY",
      name:                 "Ознаки підставної компанії",
      severity:             "Critical",
      description:          "Компанія зареєстрована нещодавно та/або має мінімальний статутний капітал.",
      isActive:             true,
      maxCompanyAgeDays:    365,   // own parameter — only on this rule type
      maxAuthorizedCapital: 10000, // own parameter — only on this rule type
      createdAt:            new Date()
    }
  },
  { upsert: true }
);

// Rule 4: Rushed deadline — has minDeadlineDays (another unique shape)
db.anomalyRules.updateOne(
  { code: "RUSHED_DEADLINE" },
  {
    $setOnInsert: {
      code:            "RUSHED_DEADLINE",
      name:            "Скорочені терміни подачі",
      severity:        "Warning",
      description:     "Строк подачі пропозицій є підозріло коротким для тендеру такого масштабу.",
      isActive:        true,
      minDeadlineDays: 7,   // own parameter — only on this rule type
      budgetThreshold: 1000000, // only relevant above this budget
      createdAt:       new Date()
    }
  },
  { upsert: true }
);

print("anomalyRules seeded.");

// ============================================================
// Seed: discussions (create first so we have IDs for auditNotes)
// ============================================================
let disc1Id = null;
const existingDisc = db.discussions.findOne({ auditNoteId: null, _initialKey: "disc-1" });
if (!existingDisc) {
  const discResult = db.discussions.insertOne({
    _initialKey: "disc-1",  // internal idempotency marker
    auditNoteId: null,       // will be set after auditNotes are created
    topic:       "Верифікація одноосібного учасника тендеру UA-2026-01-002",
    messages: [
      // EMBEDDED messages array — messages belong to this discussion and are always read together
      {
        authorId:   "auditor-01",
        authorName: "Марія Коваль",
        text:       "Тендер отримав лише одну пропозицію при очікуваній вартості 12 млн грн. Потребує перевірки.",
        createdAt:  new Date("2026-10-15T09:00:00Z")
      },
      {
        authorId:   "analyst-02",
        authorName: "Іван Шевченко",
        text:       "Перевірено: підрядник ТОВ «БудМайстер» є єдиним ліцензованим виконавцем у регіоні для цього CPV-коду. Рекомендую залишити статус Warning.",
        createdAt:  new Date("2026-10-15T14:30:00Z")
      }
    ],
    createdAt: new Date("2026-10-15T09:00:00Z"),
    updatedAt: new Date("2026-10-15T14:30:00Z")
  });
  disc1Id = discResult.insertedId;
} else {
  disc1Id = existingDisc._id;
}

print("discussions seeded.");

// ============================================================
// Seed: riskAssessments
// EMBEDDING: triggeredFlags[] — always read together with the assessment
// REFERENCING: auditNoteIds[] — notes are referenced, not embedded
// DENORMALIZATION: tenderTitle, procuringEntityName — snapshots from Bidding context
// ============================================================
db.riskAssessments.updateOne(
  { tenderId: Long("1") },
  {
    $setOnInsert: {
      tenderId: Long("1"),
      // SNAPSHOT fields — denormalized from Bidding context (P1) for read optimization
      // Consistency policy: snapshot fixed at assessment creation, NOT updated when P1 data changes
      tenderTitle:          "Відновлення школи №15 — покрівля та фасад",
      procuringEntityId:    Long("1"),
      procuringEntityName:  "Київська міська рада (Відділ відновлення)",
      overallRiskScore:     25.0,
      riskLevel:            "Low",
      // EMBEDDED triggeredFlags — no flags triggered for this healthy tender
      triggeredFlags: [],
      auditNoteIds:   [],
      assessedAt:     new Date("2026-10-10T12:00:00Z"),
      assessedBy:     "system-auto",
      createdAt:      new Date("2026-10-10T12:00:00Z"),
      updatedAt:      new Date("2026-10-10T12:00:00Z")
    }
  },
  { upsert: true }
);

db.riskAssessments.updateOne(
  { tenderId: Long("2") },
  {
    $setOnInsert: {
      tenderId: Long("2"),
      tenderTitle:         "Ремонт теплотраси — мікрорайон «Залютине»",
      procuringEntityId:   Long("2"),
      procuringEntityName: "Харківська обласна адміністрація",
      overallRiskScore:    72.5,
      riskLevel:           "High",
      // EMBEDDED triggeredFlags: two flags triggered
      triggeredFlags: [
        {
          ruleCode:      "SINGLE_BIDDER",
          severity:      "Warning",
          description:   "Тендер отримав лише одну пропозицію при очікуваній вартості 12 000 000 грн.",
          detectedAt:    new Date("2026-10-14T10:00:00Z"),
          affectedField: "bidsCount"
          // Note: no 'photos' or 'extraData' fields — demonstrates flexible schema vs other flags
        },
        {
          ruleCode:      "SHELL_COMPANY",
          severity:      "Critical",
          description:   "Єдиний учасник зареєстрований менше 2 років тому з мінімальним статутним капіталом.",
          detectedAt:    new Date("2026-10-14T10:01:00Z"),
          affectedField: "contractor.registrationDate",
          // Additional field present only on SHELL_COMPANY flag — demonstrates flexible schema
          contractorId:  101,
          companyAgeDays: 320,
          authorizedCapital: 10000
        }
      ],
      // REFERENCED auditNoteIds — will be populated after auditNotes are inserted
      auditNoteIds: [],
      assessedAt:   new Date("2026-10-14T10:05:00Z"),
      assessedBy:   "system-auto",
      createdAt:    new Date("2026-10-14T10:05:00Z"),
      updatedAt:    new Date("2026-10-14T10:05:00Z")
    }
  },
  { upsert: true }
);

print("riskAssessments seeded.");

// ============================================================
// Seed: auditNotes
// EMBEDDING: smallAttachments[] — small inline attachments
// REFERENCING: discussionId → discussions collection (hybrid approach)
// SHORT note: embeds attachments, no discussion reference needed
// LONG note: references a discussion thread (hybrid)
// ============================================================

// Get the riskAssessment _id for tender 2
const ra2 = db.riskAssessments.findOne({ tenderId: Long("2") });

if (ra2) {
  // Short note — embeds small attachment, no discussion (pure embed)
  const note1Result = db.auditNotes.updateOne(
    { riskAssessmentId: ra2._id, authorId: "auditor-01", createdAt: new Date("2026-10-14T11:00:00Z") },
    {
      $setOnInsert: {
        riskAssessmentId: ra2._id,
        authorId:   "auditor-01",
        authorName: "Марія Коваль",
        text:       "Відкрито перевірку. Запит документів надіслано підряднику ТОВ «БудМайстер».",
        noteType:   "Request",
        // EMBEDDED small attachment — always read together with the note
        smallAttachments: [
          { name: "Запит_документів.pdf", url: "https://internal.gov.ua/docs/req-001.pdf" }
        ],
        // No discussionId — this is a short note (embedding only, no reference)
        isPinned:   true,
        isResolved: false,
        createdAt:  new Date("2026-10-14T11:00:00Z"),
        updatedAt:  new Date("2026-10-14T11:00:00Z")
      }
    },
    { upsert: true }
  );

  // Long note — references a discussion thread (hybrid approach)
  const note2Result = db.auditNotes.updateOne(
    { riskAssessmentId: ra2._id, authorId: "analyst-02", createdAt: new Date("2026-10-15T09:05:00Z") },
    {
      $setOnInsert: {
        riskAssessmentId: ra2._id,
        authorId:   "analyst-02",
        authorName: "Іван Шевченко",
        text:       "Аналіз підтверджує одноосібне подання. Відкрито обговорення для опрацювання рішення.",
        noteType:   "Observation",
        // EMBEDDED small attachments
        smallAttachments: [
          { name: "Аналіз_конкуренції.xlsx", url: "https://internal.gov.ua/docs/analysis-001.xlsx" }
        ],
        // REFERENCE to discussions collection — hybrid approach for large threads
        discussionId: disc1Id,
        isPinned:   false,
        isResolved: false,
        createdAt:  new Date("2026-10-15T09:05:00Z"),
        updatedAt:  new Date("2026-10-15T09:05:00Z")
      }
    },
    { upsert: true }
  );

  // Retrieve inserted IDs and link them to riskAssessment
  const note1 = db.auditNotes.findOne({ riskAssessmentId: ra2._id, authorId: "auditor-01" });
  const note2 = db.auditNotes.findOne({ riskAssessmentId: ra2._id, authorId: "analyst-02" });

  if (note1 && note2) {
    db.riskAssessments.updateOne(
      { _id: ra2._id },
      { $addToSet: { auditNoteIds: { $each: [note1._id, note2._id] } } }
    );

    // Also link the discussion back to its auditNote
    db.discussions.updateOne(
      { _id: disc1Id },
      { $set: { auditNoteId: note2._id } }
    );
  }
}

print("auditNotes seeded and linked to riskAssessments.");
print("All seed data inserted successfully. Re-running this script is safe (idempotent).");
