# Bounded Contexts — Automated Anomaly Detection in Procurement

## Domain Overview

The system automatically analyses public reconstruction procurement tenders published through Ukraine's Prozorro platform, cross-references participant (contractor) profiles against business registries, and produces risk scores and anomaly flags to assist auditors and anti-corruption investigators.

Three independent bounded contexts are identified, each with its own database, data model, and life cycle:

---

## Context 1 — Bidding / Procurement Process (Project №1)

**Technology:** SQL Server + ADO.NET & Dapper  
**Database:** `ProcurementBidding`  
**Why SQL:** The tendering process is strictly transactional — a tender passes through a defined status machine (Draft → Published → BiddingOpen → EvaluationInProgress → Awarded / Cancelled). Stored procedures with explicit transactions ensure that illegal status transitions are rejected and that bid submission is atomic.

### Entities

| Entity | Role |
|--------|------|
| `ProcuringEntity` | State agency, city council, or municipality that publishes tenders |
| `Tender` | A public call for bids for reconstruction work; owns the status machine |
| `TenderDetails` | 1:1 extended description attached to a Tender |
| `Contractor` *(local snapshot)* | Read-model copy of Contractor from Registry context — plain `Id` column, **no FK** to Registry DB |
| `Bid` | A contractor's price proposal for a specific tender (M:N junction with snapshot fields) |
| `TenderStatusHistory` | Audit log of every status transition |

### Relationships

- **1:1** — `Tender` ↔ `TenderDetails` (FK on `TenderDetails.TenderId` is also its PK)
- **1:N** — `ProcuringEntity` → `Tender` (FK `ProcuringEntityId` on the many side)
- **M:N** — `Tender` ↔ `Contractor` via `Bid` (composite PK on both FK columns)

---

## Context 2 — Contractor Registry (Project №2)

**Technology:** SQL Server + EF Core (Code First, Fluent API)  
**Database:** `ContractorRegistry`  
**Why EF Core:** The registry is a rich relational catalog with many entities and relationship types. Code First with Fluent API makes it straightforward to manage migrations as the schema evolves (Lab 03).

### Entities

| Entity | Role |
|--------|------|
| `Contractor` | Private company or individual registered to perform reconstruction work |
| `ContractorProfile` | 1:1 extended contact/description data |
| `Region` | Reference table of Ukrainian oblasts |
| `ServiceCategory` | CPV/DK021 procurement categories (self-referential for hierarchy) |
| `ContractorServiceCategory` | M:N junction: contractor ↔ category with own fields (`CertifiedAt`, `ExpiresAt`, `CertificateNumber`) |
| `ContractorLicense` | License issued to a contractor for a specific region (1:N from Contractor, N:1 to Region) |

### Relationships

- **1:1** — `Contractor` ↔ `ContractorProfile`
- **1:N** — `Contractor` → `ContractorLicense`
- **M:N** — `Contractor` ↔ `ServiceCategory` via `ContractorServiceCategory` (junction with own fields)

---

## Context 3 — Risk Assessment & Anomaly Flags (Project №3)

**Technology:** MongoDB  
**Database:** `RiskAssessment`  
**Why MongoDB:** Risk assessments have a flexible structure — different anomaly rules produce flags with different parameter shapes. The document model naturally accommodates this variability. Audit note threads can grow unboundedly, making embedding impractical; referencing is used instead.

### Collections

| Collection | Role |
|------------|------|
| `riskAssessments` | Per-tender assessment result with embedded `triggeredFlags[]` and referenced `auditNoteIds[]` |
| `anomalyRules` | Catalog of detection rules; **flexible schema** — each rule type has a different parameter shape |
| `auditNotes` | Auditor annotations; **hybrid** — small attachments embedded, large discussion threads referenced |
| `discussions` | Threaded discussion messages referenced from `auditNotes.discussionId` |

### Modeling approaches

| Approach | Where used | Rationale |
|----------|-----------|-----------|
| **Embedding** | `riskAssessments.triggeredFlags[]` | Flags are always read together with the assessment; tight coupling |
| **Embedding** | `auditNotes.smallAttachments[]` | Small inline links/filenames, always needed with the note |
| **Embedding** | `discussions.messages[]` | Messages belong to one discussion; paginated but typically read together |
| **Referencing** | `riskAssessments.auditNoteIds[]` → `auditNotes` | Notes can be large; kept separate to avoid document bloat |
| **Referencing** | `auditNotes.discussionId` → `discussions` | Discussions can grow to thousands of messages — referencing avoids the 16 MB document limit |
| **Hybrid** | `auditNotes` | Short notes embed everything; long notes also reference a discussion thread |
| **Flexible schema** | `anomalyRules` | `SINGLE_BIDDER` has no numeric params; `PRICE_DUMPING` adds `maxPriceRatioPct`; `SHELL_COMPANY` adds `maxCompanyAgeDays` + `maxAuthorizedCapital`; `RUSHED_DEADLINE` adds `minDeadlineDays` + `budgetThreshold` |
