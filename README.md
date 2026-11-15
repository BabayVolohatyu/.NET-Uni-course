# Automated Anomaly Detection & Risk Assessment in Public Reconstruction Procurement

## Domain Description

Ukraine's post-war reconstruction involves billions of hryvnias flowing through public procurement (Prozorro). The speed of reconstruction means thousands of tenders are published simultaneously, making manual auditing impossible. This system **automatically analyses tenders, participants (contractors), and historical patterns to detect corruption risks, collusion, and operational anomalies**, producing a risk score and human-readable flags that auditors can act on immediately.

Core actors: **Procuring Entity** (state agency, city council, municipality), **Tender** (a public call for bids), **Contractor** (private company submitting a bid), **Bid** (price proposal), **Risk Assessment** (the system's analytical verdict with anomaly flags).

---

## Three Bounded Contexts

Each context has its own independent database. There are **no FOREIGN KEYs across databases** — references between contexts use plain ID columns only. Data needed across contexts is explicitly duplicated per the policy table below.

| # | Context | Database | Technology | Focus |
|---|---------|----------|------------|-------|
| **P1** | Bidding / Procurement Process | `ProcurementBidding` (SQL Server) | ADO.NET & Dapper | Tender lifecycle, bids, transactional status changes |
| **P2** | Contractor Registry | `ContractorRegistry` (SQL Server) | EF Core Code First | Contractor qualifications, licenses, categories |
| **P3** | Risk Assessment & Anomaly Flags | `RiskAssessment` (MongoDB) | MongoDB Driver | Risk scores, anomaly flags, auditor notes, discussions |

### Context 1 — Bidding / Procurement Process (P1)

Entities: `ProcuringEntity`, `Tender`, `TenderDetails` (1:1), `Contractor` *(local snapshot)*, `Bid` (M:N), `TenderStatusHistory`

Relationships: 1:1 Tender↔TenderDetails · 1:N ProcuringEntity→Tender · M:N Tender↔Contractor via Bid

### Context 2 — Contractor Registry (P2)

Entities: `Contractor`, `ContractorProfile` (1:1), `Region`, `ServiceCategory`, `ContractorServiceCategory` (M:N junction with own fields), `ContractorLicense`

Relationships: 1:1 Contractor↔ContractorProfile · 1:N Contractor→ContractorLicense · M:N Contractor↔ServiceCategory via ContractorServiceCategory

### Context 3 — Risk Assessment & Anomaly Flags (P3)

Collections: `riskAssessments`, `anomalyRules`, `auditNotes`, `discussions`

Modeling: embedding (triggeredFlags), referencing (auditNoteIds, discussionId), hybrid (auditNotes), flexible schema (anomalyRules)

---

## Duplication Policy

No FOREIGN KEY constraint is created between databases. Each duplicated field below is owned by one service; the consuming service stores a plain copy.

| Duplicated Field | Stored In | Source (Owner) | Consistency Policy |
|-----------------|-----------|----------------|--------------------|
| `Bids.ContractorName` | `ProcurementBidding` DB (P1) | `Contractor` in `ContractorRegistry` (P2) | **Snapshot** — fixed at bid submission time, never updated. Reflects the contractor's name as known when the bid was placed (same principle as `OrderItem.ProductName` in Microsoft eShop). |
| `Bids.ContractorEDRPOU` | `ProcurementBidding` DB (P1) | `Contractor` in `ContractorRegistry` (P2) | **Snapshot** — fixed at bid submission time. |
| `Contractors.Name` *(local copy)* | `ProcurementBidding` DB (P1) | `Contractor` in `ContractorRegistry` (P2) | **Event-updated copy** — updated when Registry publishes a `ContractorNameChanged` event (eventual consistency via messaging, implemented in Lab 08). |
| `Contractors.EDRPOU` *(local copy)* | `ProcurementBidding` DB (P1) | `Contractor` in `ContractorRegistry` (P2) | **Event-updated copy** — same event as above. |
| `riskAssessments.tenderTitle` | `RiskAssessment` DB (P3) | `Tender` in `ProcurementBidding` (P1) | **Snapshot** — fixed at assessment creation time. The assessment records what was known at evaluation time. |
| `riskAssessments.procuringEntityName` | `RiskAssessment` DB (P3) | `ProcuringEntity` in `ProcurementBidding` (P1) | **Snapshot** — fixed at assessment creation time. |

> **Why no FK?** A `FOREIGN KEY` between two SQL Server databases would require a linked server or cross-database constraint, coupling the services at the infrastructure level. Microservice autonomy requires each DB to be independently deployable. Cross-context integrity is enforced by application code, not by the database engine.

---

## ERD Diagrams

See [`docs/erd.md`](docs/erd.md) for full ASCII ERD diagrams of all three projects.  
See [`docs/contexts.md`](docs/contexts.md) for detailed context and entity descriptions.

Online ERD (dbdiagram.io):

**Project №1 (ProcurementBidding):**
```
Table ProcuringEntities { Id bigint [pk, increment]; Name nvarchar; EDRPOU char [unique]; Region nvarchar; }
Table Tenders { Id bigint [pk, increment]; ProcuringEntityId bigint [ref: > ProcuringEntities.Id]; Title nvarchar; ExpectedBudget decimal; Status nvarchar; }
Table TenderDetails { TenderId bigint [pk, ref: - Tenders.Id]; Description nvarchar; }
Table Contractors { Id bigint [pk]; Name nvarchar; EDRPOU char; Note: "plain Id — no FK to Registry" }
Table Bids { TenderId bigint [ref: > Tenders.Id]; ContractorId bigint [ref: > Contractors.Id]; ProposedPrice decimal; ContractorName nvarchar [note: "snapshot"]; indexes { (TenderId, ContractorId) [pk] } }
Table TenderStatusHistory { Id bigint [pk, increment]; TenderId bigint [ref: > Tenders.Id]; FromStatus nvarchar; ToStatus nvarchar; }
```

**Project №2 (ContractorRegistry):**
```
Table Contractors { Id bigint [pk, increment]; Name nvarchar; EDRPOU char [unique]; LegalForm nvarchar; }
Table ContractorProfiles { ContractorId bigint [pk, ref: - Contractors.Id]; Website nvarchar; }
Table Regions { Id int [pk, increment]; Name nvarchar; Code nvarchar [unique]; }
Table ServiceCategories { Id int [pk, increment]; Code nvarchar [unique]; ParentId int [ref: > ServiceCategories.Id]; }
Table ContractorServiceCategories { ContractorId bigint [ref: > Contractors.Id]; ServiceCategoryId int [ref: > ServiceCategories.Id]; CertifiedAt date; ExpiresAt date; CertificateNumber nvarchar; indexes { (ContractorId, ServiceCategoryId) [pk] } }
Table ContractorLicenses { Id int [pk, increment]; ContractorId bigint [ref: > Contractors.Id]; RegionId int [ref: > Regions.Id]; LicenseNumber nvarchar [unique]; }
```

---

## Deployment Instructions

### Prerequisites

- Docker Desktop running on Windows
- PowerShell
- `mongosh` CLI (for MongoDB scripts) — download from https://www.mongodb.com/try/download/shell

### 1. Start databases with Docker

```powershell
docker compose up -d
```

This starts SQL Server on port **1433** and MongoDB on port **27017**.

If `docker-compose.yml` is not yet present, run containers manually:

```powershell
# SQL Server
docker run -d --name sqlserver `
  -e "ACCEPT_EULA=Y" `
  -e "SA_PASSWORD=YourStrong!Passw0rd" `
  -p 1433:1433 `
  mcr.microsoft.com/mssql/server:2022-latest

# MongoDB
docker run -d --name mongodb `
  -p 27017:27017 `
  mongo:7
```

Wait ~15 seconds for SQL Server to start before running scripts.

### 2. Apply Project №1 — ProcurementBidding

```powershell
# From repo root — requires sqlcmd on PATH or via Docker exec
sqlcmd -S localhost,1433 -U SA -P "YourStrong!Passw0rd" -i db\p1\schema.sql
sqlcmd -S localhost,1433 -U SA -P "YourStrong!Passw0rd" -i db\p1\procedures.sql
sqlcmd -S localhost,1433 -U SA -P "YourStrong!Passw0rd" -i db\p1\seed.sql
```

Or via Docker exec (no local sqlcmd needed):

```powershell
docker cp db\p1\schema.sql     sqlserver:/tmp/schema.sql
docker cp db\p1\procedures.sql sqlserver:/tmp/procedures.sql
docker cp db\p1\seed.sql       sqlserver:/tmp/seed.sql

docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/schema.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/procedures.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/seed.sql
```

### 3. Apply Project №2 — ContractorRegistry

```powershell
docker cp db\p2\schema.sql sqlserver:/tmp/p2_schema.sql
docker cp db\p2\seed.sql   sqlserver:/tmp/p2_seed.sql

docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/p2_schema.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/p2_seed.sql
```

### 4. Apply Project №3 — RiskAssessment (MongoDB)

```powershell
mongosh mongodb://localhost:27017 db\p3\collections.js
mongosh mongodb://localhost:27017 db\p3\seed.js
```

### 5. Verify

```powershell
# P1: check tables
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No `
  -Q "USE ProcurementBidding; SELECT name FROM sys.tables ORDER BY name;"

# P1: verify no cross-DB FK (should return 0 rows)
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No `
  -Q "USE ProcurementBidding; SELECT name FROM sys.foreign_keys;"

# P1: test invalid status transition (should throw error 50002)
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No `
  -Q "USE ProcurementBidding; EXEC dbo.usp_ChangeTenderStatus @TenderId=1, @NewStatus=N'Shipped';"

# P1: test awarded tender cannot be cancelled (should throw error 50002)
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No `
  -Q "USE ProcurementBidding; EXEC dbo.usp_ChangeTenderStatus @TenderId=1, @NewStatus=N'Cancelled';"

# P2: check tables
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "YourStrong!Passw0rd" -No `
  -Q "USE ContractorRegistry; SELECT name FROM sys.tables ORDER BY name;"

# P3: check collections
mongosh mongodb://localhost:27017/RiskAssessment --eval "db.getCollectionNames()"
mongosh mongodb://localhost:27017/RiskAssessment --eval "db.riskAssessments.find().pretty()"
```

### Re-running scripts

All seed scripts are **idempotent**:
- SQL scripts use `IF NOT EXISTS` before every `INSERT`.
- MongoDB scripts use `updateOne` with `upsert: true`.

Running any script a second time produces no duplicate records.

---

## Connection Strings

| DB | Connection String |
|----|------------------|
| ProcurementBidding | `Server=localhost,1433;Database=ProcurementBidding;User Id=SA;Password=YourStrong!Passw0rd;TrustServerCertificate=True` |
| ContractorRegistry | `Server=localhost,1433;Database=ContractorRegistry;User Id=SA;Password=YourStrong!Passw0rd;TrustServerCertificate=True` |
| RiskAssessment | `mongodb://localhost:27017/RiskAssessment` |

---

## Lab Roadmap

| Lab | Context | Focus |
|-----|---------|-------|
| **01** (this lab) | All 3 | Schema design, seed scripts, stored procedures |
| **02** | P1 — ProcurementBidding | ADO.NET + Dapper, three-layer architecture |
| **03** | P2 — ContractorRegistry | EF Core Code First, Fluent API, migrations |
| **04** | P3 — RiskAssessment | MongoDB driver, clean architecture |
