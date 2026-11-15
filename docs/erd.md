# ERD Diagrams

## Project №1 — ProcurementBidding (SQL Server)

```
┌───────────────────────────┐         ┌───────────────────────────────┐
│ ProcuringEntities          │         │ Tenders                        │
│---------------------------│         │-------------------------------│
│ PK Id          BIGINT     │ 1     N │ PK Id            BIGINT       │
│    Name        NVARCHAR   │◄────────│ FK ProcuringEntityId BIGINT   │
│    EDRPOU      CHAR(8) UQ │         │    Title         NVARCHAR     │
│    Region      NVARCHAR   │         │    ExpectedBudget DECIMAL     │
│    ContactEmail NVARCHAR  │         │    Status        NVARCHAR CK  │
│    CreatedAt   DATETIME2  │         │    PublishedAt   DATETIME2    │
│    CreatedBy   NVARCHAR   │         │    BiddingDeadline DATETIME2  │
│    UpdatedAt   DATETIME2  │         │    CreatedAt     DATETIME2    │
│    UpdatedBy   NVARCHAR   │         │    CreatedBy     NVARCHAR     │
│    IsDeleted   BIT        │         │    UpdatedAt     DATETIME2    │
│    RowVersion  ROWVERSION │         │    UpdatedBy     NVARCHAR     │
└───────────────────────────┘         │    IsDeleted     BIT         │
                                       │    RowVersion    ROWVERSION  │
                                       └───────────────┬───────────────┘
                                                       │ 1
                        ┌──────────────────────────────┤
                        │                              │
                        ▼ 1 (FK=PK)                   ▼ N
         ┌──────────────────────────┐  ┌──────────────────────────────┐
         │ TenderDetails             │  │ TenderStatusHistory           │
         │--------------------------│  │------------------------------│
         │ PK/FK TenderId  BIGINT   │  │ PK Id         BIGINT         │
         │    Description  NVARCHAR │  │ FK TenderId   BIGINT         │
         │    CategoryCode NVARCHAR │  │    FromStatus NVARCHAR       │
         │    DeliveryAddr NVARCHAR │  │    ToStatus   NVARCHAR       │
         │    TechSpecUrl  NVARCHAR │  │    ChangedAt  DATETIME2      │
         │    CreatedAt    DATETIME2│  │    ChangedBy  NVARCHAR       │
         └──────────────────────────┘  │    Reason     NVARCHAR       │
                                        └──────────────────────────────┘

              ┌────────────────────────────────────┐
              │ Bids  (M:N junction)                │
              │------------------------------------│
         N    │ PK/FK TenderId    BIGINT           │  N
Tenders ──────│ PK/FK ContractorId BIGINT ─────────│────── Contractors
              │    ContractorName  NVARCHAR (snap) │       (local snapshot)
              │    ContractorEDRPOU CHAR(8) (snap) │
              │    ProposedPrice   DECIMAL         │
              │    PriceReductionPct DECIMAL       │
              │    SubmittedAt    DATETIME2        │
              │    IsWinner       BIT              │
              │    IsDisqualified BIT              │
              │    CreatedAt      DATETIME2        │
              │    IsDeleted      BIT              │
              │    RowVersion     ROWVERSION       │
              └────────────────────────────────────┘

              ┌────────────────────────────────────┐
              │ Contractors  (local read-model)     │
              │------------------------------------│
              │ PK Id    BIGINT  ← same as Registry │
              │    Name  NVARCHAR  (from Registry)  │
              │    EDRPOU CHAR(8)  (from Registry)  │
              │    CreatedAt DATETIME2              │
              │    UpdatedAt DATETIME2              │
              │    ⚠ NO FK to Registry DB           │
              └────────────────────────────────────┘
```

**Relationship summary (P1):**

| Relationship | Type | FK placement |
|-------------|------|-------------|
| ProcuringEntity → Tender | 1:N | `Tenders.ProcuringEntityId` |
| Tender ↔ TenderDetails | 1:1 | `TenderDetails.TenderId` (FK = PK) |
| Tender → TenderStatusHistory | 1:N | `TenderStatusHistory.TenderId` |
| Tender ↔ Contractor via Bid | M:N | Composite PK `(TenderId, ContractorId)` on Bids |

---

## Project №2 — ContractorRegistry (SQL Server)

```
┌────────────────────────────────┐         ┌───────────────────────────────┐
│ Contractors                     │         │ ContractorProfiles             │
│--------------------------------│    1:1  │-------------------------------│
│ PK Id              BIGINT      │◄────────│ PK/FK ContractorId BIGINT     │
│    Name            NVARCHAR    │         │    Website         NVARCHAR   │
│    EDRPOU          CHAR(8) UQ  │         │    ContactEmail    NVARCHAR   │
│    LegalForm       NVARCHAR CK │         │    ContactPhone    NVARCHAR   │
│    RegistrationDate DATE       │         │    HQAddress       NVARCHAR   │
│    AuthorizedCapital DECIMAL   │         │    Description     NVARCHAR   │
│    IsActive        BIT         │         └───────────────────────────────┘
│    CreatedAt       DATETIME2   │
│    IsDeleted       BIT         │  1
│    RowVersion      ROWVERSION  │──────────────────────┐
└────────────────────────────────┘                      │ N
         │                                 ┌────────────────────────────────┐
         │ M:N via ContractorServiceCategories│ ContractorLicenses            │
         │                                 │--------------------------------│
┌────────▼─────────────────────────────┐  │ PK Id            INT           │
│ ContractorServiceCategories  (M:N)   │  │ FK ContractorId  BIGINT        │
│--------------------------------------│  │ FK RegionId      INT           │
│ PK FK ContractorId     BIGINT        │  │    LicenseNumber NVARCHAR UQ   │
│ PK FK ServiceCategoryId INT          │  │    IssuedAt      DATE          │
│    CertifiedAt          DATE  *own*  │  │    ValidUntil    DATE          │
│    ExpiresAt            DATE  *own*  │  │    IsActive      BIT           │
│    CertificateNumber    NVARCHAR *own│  └────────────────────────────────┘
└──────────────────────────────────────┘             │ N:1
         │ N:1                                       │
┌────────▼──────────────────┐           ┌────────────▼──────────────────┐
│ ServiceCategories          │           │ Regions                        │
│---------------------------│           │-------------------------------│
│ PK Id    INT              │           │ PK Id   INT                   │
│    Name  NVARCHAR         │           │    Name NVARCHAR               │
│    Code  NVARCHAR  UQ     │           │    Code NVARCHAR  UQ           │
│    ParentId INT (self-ref)│           └───────────────────────────────┘
└───────────────────────────┘
```

**Relationship summary (P2):**

| Relationship | Type | FK placement |
|-------------|------|-------------|
| Contractor ↔ ContractorProfile | 1:1 | `ContractorProfiles.ContractorId` (FK = PK) |
| Contractor → ContractorLicense | 1:N | `ContractorLicenses.ContractorId` |
| ContractorLicense → Region | N:1 | `ContractorLicenses.RegionId` |
| Contractor ↔ ServiceCategory via ContractorServiceCategory | M:N | Composite PK on junction; own fields: `CertifiedAt`, `ExpiresAt`, `CertificateNumber` |

---

## Project №3 — RiskAssessment (MongoDB)

```
riskAssessments                        auditNotes
┌────────────────────────────────┐     ┌──────────────────────────────────┐
│ _id: ObjectId                  │     │ _id: ObjectId                    │
│ tenderId: Long  ──→ P1 (no FK) │     │ riskAssessmentId: ObjectId ──────┤
│ tenderTitle: String (snapshot) │     │ authorId / authorName: String    │
│ procuringEntityId: Long        │     │ text: String                     │
│ procuringEntityName: String    │     │ noteType: String                 │
│   (snapshot)                   │     │ smallAttachments: [              │
│ overallRiskScore: Double       │     │   { name, url }  ← EMBEDDED      │
│ riskLevel: String              │     │ ]                                │
│ triggeredFlags: [              │     │ discussionId: ObjectId ──────────┤──┐
│  { ruleCode, severity,         │     │   (REFERENCE to discussions)     │  │
│    description, detectedAt,    │     │ isPinned / isResolved: Bool      │  │
│    affectedField,              │     │ createdAt / updatedAt: Date      │  │
│    ...extra (flexible) }       │     └──────────────────────────────────┘  │
│ ] ← EMBEDDED                   │                                            │
│ auditNoteIds: [ ObjectId ]─────┼──→ (REFERENCE to auditNotes)              │
│   (REFERENCES)                 │                                            │
│ assessedAt / createdAt: Date   │     discussions                            │
└────────────────────────────────┘     ┌──────────────────────────────────┐  │
                                        │ _id: ObjectId  ◄─────────────────┼──┘
anomalyRules (flexible schema)          │ auditNoteId: ObjectId            │
┌────────────────────────────────┐      │ topic: String                    │
│ SINGLE_BIDDER:                 │      │ messages: [                      │
│  { code, name, severity }      │      │   { authorId, authorName,        │
│                                │      │     text, createdAt }            │
│ PRICE_DUMPING:                 │      │ ] ← EMBEDDED                     │
│  { code, ..., maxPriceRatioPct}│      │ createdAt / updatedAt: Date      │
│                                │      └──────────────────────────────────┘
│ SHELL_COMPANY:                 │
│  { code, ..., maxCompanyAgeDays│
│    maxAuthorizedCapital }      │
│                                │
│ RUSHED_DEADLINE:               │
│  { code, ..., minDeadlineDays, │
│    budgetThreshold }           │
└────────────────────────────────┘
```

**Modeling summary (P3):**

| Technique | Where | Rationale |
|-----------|-------|-----------|
| Embedding | `riskAssessments.triggeredFlags[]` | Flags always read with assessment; tight coupling; bounded size |
| Embedding | `auditNotes.smallAttachments[]` | Always needed; small size |
| Embedding | `discussions.messages[]` | Messages belong to one discussion |
| Referencing | `riskAssessments.auditNoteIds[]` | Notes can be large; independent lifecycle |
| Referencing | `auditNotes.discussionId` | Threads can be huge; avoids 16 MB limit |
| Hybrid | `auditNotes` | Short notes embed all; long notes also reference discussion |
| Flexible schema | `anomalyRules` | Each rule type has different parameter fields |
| Denormalization | `tenderTitle`, `procuringEntityName` | Snapshot from P1 for read optimization |
