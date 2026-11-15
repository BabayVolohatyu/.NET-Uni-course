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
| **01** | All 3 | Schema design, seed scripts, stored procedures |
| **02** (this lab) | P1 — ProcurementBidding | ADO.NET + Dapper, three-layer architecture, Aspire |
| **03** | P2 — ContractorRegistry | EF Core Code First, Fluent API, migrations |
| **04** | P3 — RiskAssessment | MongoDB driver, clean architecture |

---

## Lab 02 — Tenders Microservice (ADO.NET + Dapper)

### Solution structure

```
Platform.slnx                     ← solution file
Platform.AppHost/                  ← .NET Aspire orchestration (single-command launch)
Services/
└── Tenders/                       ← P1 Microservice — Bidding/Procurement context
    ├── Tenders.Api/               ← Controllers, Program.cs, Swagger, ProblemDetails middleware
    ├── Tenders.Bll/               ← Services, DTOs, AutoMapper profiles, business rules
    ├── Tenders.Dal/               ← Repositories (ADO.NET + Dapper), UnitOfWork, interfaces
    └── Tenders.Domain/            ← Domain models, exceptions (NotFoundException, etc.), constants
```

Dependency direction: `Api → Bll → Dal`; `Domain` is shared by both `Dal` and `Bll`.
Controllers never touch repositories directly — only through service interfaces.

### Quickstart — Option A: via .NET Aspire (recommended)

Aspire launches the API and connects it to an existing ProcurementBidding SQL Server in one command.

**Prerequisites:**
- Docker Desktop running (SQL Server container from Lab 01)
- .NET 10 SDK

```powershell
# 1. Start SQL Server and apply Lab 01 scripts (if not already done)
docker compose up -d
# wait ~15 seconds for SQL Server to start

docker cp db\p1\schema.sql     sqlserver:/tmp/schema.sql
docker cp db\p1\procedures.sql sqlserver:/tmp/procedures.sql
docker cp db\p1\seed.sql       sqlserver:/tmp/seed.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/schema.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/procedures.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "YourStrong!Passw0rd" -No -i /tmp/seed.sql

# 2. Launch via Aspire (starts API + injects DB connection string automatically)
dotnet run --project Platform.AppHost
```

Open the Aspire Dashboard URL printed in the console (typically `http://localhost:15888`).  
Click the `tenders-api` row → open the API endpoint → navigate to `/swagger`.

### Quickstart — Option B: direct API launch (without Aspire)

```powershell
# 1. Start SQL Server and apply scripts (same as above)

# 2. Run the API directly
dotnet run --project Services\Tenders\Tenders.Api

# Open Swagger
start http://localhost:5000/swagger
```

### API endpoints summary

| Method | URL | Description | Success | Error |
|--------|-----|-------------|---------|-------|
| `GET` | `/api/procuring-entities` | List all procuring entities | 200 | — |
| `GET` | `/api/procuring-entities/{id}` | Get entity by ID | 200 | 404 |
| `POST` | `/api/procuring-entities` | Create procuring entity | 201 | 400, 409 |
| `GET` | `/api/tenders/{id}` | Get tender with details | 200 | 404 |
| `GET` | `/api/tenders/by-entity/{id}?status=Draft` | Tenders for an entity | 200 | — |
| `POST` | `/api/tenders` | Create tender (Draft status) | 201 | 400, 404 |
| `POST` | `/api/tenders/{id}/change-status` | Change tender status | 204 | 400, 404, 409 |
| `GET` | `/api/tenders/{id}/status-history` | Status audit log | 200 | — |
| `GET` | `/api/tenders/{id}/bids` | List bids for a tender | 200 | — |
| `POST` | `/api/tenders/{id}/bids` | Submit a bid | 201 | 400, 404, 409 |
| `POST` | `/api/tenders/{id}/bids/award` | Award tender to winner | 204 | 400, 404, 409 |

### Sample curl requests

```bash
# Create a procuring entity
curl -X POST http://localhost:5000/api/procuring-entities \
  -H "Content-Type: application/json" \
  -d '{"name":"Kyiv City Council","edrpou":"12345678","region":"Kyiv","contactEmail":"tender@kyivcity.gov.ua"}'

# Create a tender (use the returned entity Id)
curl -X POST http://localhost:5000/api/tenders \
  -H "Content-Type: application/json" \
  -d '{"procuringEntityId":1,"title":"Road repair Khreshchatyk 2024","expectedBudget":5000000,"description":"Full road surface repair","deliveryAddress":"Kyiv, Khreshchatyk St 1"}'

# Advance tender to Published
curl -X POST http://localhost:5000/api/tenders/1/change-status \
  -H "Content-Type: application/json" \
  -d '{"newStatus":"Published","reason":"Approved by committee"}'

# Submit a bid (contractor must exist in Contractors table)
curl -X POST http://localhost:5000/api/tenders/1/bids \
  -H "Content-Type: application/json" \
  -d '{"contractorId":1,"proposedPrice":4800000,"priceReductionPct":4}'

# Try invalid transition — returns 409 ProblemDetails
curl -X POST http://localhost:5000/api/tenders/1/change-status \
  -H "Content-Type: application/json" \
  -d '{"newStatus":"Awarded"}'

# Try non-existent tender — returns 404 ProblemDetails
curl http://localhost:5000/api/tenders/99999
```

### Validation self-check

```powershell
# 1. Special character in name (SQL injection prevention)
curl -X POST http://localhost:5000/api/procuring-entities `
  -H "Content-Type: application/json" `
  -d '{"name":"O'\''Brien Infrastructure","edrpou":"99887766","region":"Lviv"}'
# Should succeed — O'Brien is handled correctly via parameterized SQL

# 2. Invalid status transition — 409 ProblemDetails
curl -X POST http://localhost:5000/api/tenders/1/change-status `
  -H "Content-Type: application/json" `
  -d '{"newStatus":"Awarded"}'
# Returns: 409 Conflict with ProblemDetails.detail = "Invalid status transition..."

# 3. Non-existent ID — 404 ProblemDetails
curl http://localhost:5000/api/tenders/999999
# Returns: 404 Not Found with ProblemDetails, NOT 500

# 4. Award tender (transactional — rolls back both bid+status if it fails)
curl -X POST http://localhost:5000/api/tenders/1/bids/award `
  -H "Content-Type: application/json" `
  -d '{"winnerContractorId":1}'
```

### Architecture decisions

**Isolation level:** `ReadCommitted` for all transactions. Prevents dirty reads while keeping lock contention low. `WITH (UPDLOCK)` in stored procedures guards against race conditions on the same tender row.

**Connection pooling:** SqlClient connection pooling is enabled by default. `UnitOfWork.DisposeAsync()` returns the connection to the pool rather than physically closing it.

**Duplication policy:** See the table in the main section above. `Bids.ContractorName` and `Bids.ContractorEDRPOU` are immutable snapshots — never updated after submission (same pattern as `OrderItem.ProductName` in Microsoft eShop).
