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
| **04** | P3 — RiskAssessment | Clean Architecture, MediatR CQRS, MongoDB |

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

---

## Lab 03 — Catalog (Contractor Registry) Microservice (EF Core)

### Solution structure

```
Platform.slnx                     ← solution file (updated)
Services/
├── Tenders/                       ← P1 Microservice (Lab 02, unchanged)
└── Catalog/                       ← P2 Microservice — Contractor Registry context (Lab 03)
    ├── Catalog.Api/               ← Controllers, Program.cs, Swagger, exception handler, Dockerfile
    ├── Catalog.Bll/               ← Services, DTOs, AutoMapper, Specifications, FluentValidation
    ├── Catalog.Dal/               ← DbContext, Fluent API Configurations, Migrations, Repositories, UoW, Seeding
    └── Catalog.Domain/            ← Entities (Contractor, ContractorDetail, ContractorDocument,
                                      WorkCategory, ContractorWorkCategory), exceptions
```

### Domain model

| Entity | Relationship | Description |
|--------|-------------|-------------|
| `Contractor` | Aggregate root | Company registered in state business registry (EDRPOU) |
| `ContractorDetail` | 1:1 ← Contractor | Extended details (address, director, contact) |
| `ContractorDocument` | 1:N ← Contractor | Qualification documents and licenses |
| `WorkCategory` | Reference table | Types of reconstruction work (ROAD, BLDG, ELEC, PIPE, HEAT) |
| `ContractorWorkCategory` | M:N junction | Contractor ↔ WorkCategory with QualificationDate |

### Three loading patterns demonstrated

| Repository | Loading Pattern | SQL queries |
|------------|----------------|-------------|
| `ContractorRepository.GetWithDetailsAsync` | **Eager Loading** (Include/ThenInclude) | 1 (JOINs) |
| `WorkCategoryRepository.GetWithContractorsAsync` | **Explicit Loading** (Entry.Collection.LoadAsync) | 2 |
| `ContractorWorkCategoryRepository.GetRecentlyQualifiedAsync` | **LINQ M:N** (through junction) | 1 |

### Quickstart — Lab 03 Catalog service

```powershell
# 1. Start SQL Server (shared with Lab 02)
docker compose up -d
# Wait ~30 seconds for SQL Server to be ready

# 2. Apply EF Core migrations (creates ContractorRegistry DB, tables, HasData reference seeds)
dotnet ef database update `
    --project Services\Catalog\Catalog.Dal `
    --startup-project Services\Catalog\Catalog.Api

# 3. Run the Catalog API (migrations + idempotent seeding run on startup automatically)
dotnet run --project Services\Catalog\Catalog.Api

# Open Swagger UI
start http://localhost:5002/swagger
```

### API endpoints summary

| Method | URL | Description | Success | Error |
|--------|-----|-------------|---------|-------|
| `GET` | `/api/contractors?page=1&pageSize=20&search=` | Paged list with filters | 200 | — |
| `GET` | `/api/contractors/{id}` | Full contractor card (Eager Loading) | 200 | 404 |
| `POST` | `/api/contractors` | Create contractor (checks EDRPOU uniqueness) | 201 | 400, 409 |
| `PUT` | `/api/contractors/{id}` | Update name and capital | 200 | 400, 404 |
| `DELETE` | `/api/contractors/{id}` | Remove contractor | 204 | 404 |
| `PATCH` | `/api/contractors/{id}/activate` | Activate contractor | 204 | 404, 409 |
| `PATCH` | `/api/contractors/{id}/deactivate` | Deactivate contractor | 204 | 404, 409 |
| `GET` | `/api/work-categories` | All work categories | 200 | — |
| `GET` | `/api/work-categories/{id}` | Category by ID | 200 | 404 |
| `GET` | `/api/work-categories/by-code/{code}` | Category by code (e.g. ROAD) | 200 | 404 |

### Self-check commands

```powershell
# 1. Create contractor (201 Created + Location header)
curl -X POST http://localhost:5002/api/contractors `
  -H "Content-Type: application/json" `
  -d '{"name":"ТОВ Нова Компанія","edrpou":"99988877","authorizedCapital":500000,"registrationDate":"2022-01-15T00:00:00Z"}'

# 2. Duplicate EDRPOU → 409 Conflict
curl -X POST http://localhost:5002/api/contractors `
  -H "Content-Type: application/json" `
  -d '{"name":"Дублікат","edrpou":"99988877","authorizedCapital":100000,"registrationDate":"2022-01-01T00:00:00Z"}'

# 3. Invalid input (empty name) → 400 ValidationProblemDetails
curl -X POST http://localhost:5002/api/contractors `
  -H "Content-Type: application/json" `
  -d '{"name":"","edrpou":"12341234","authorizedCapital":100000,"registrationDate":"2022-01-01T00:00:00Z"}'

# 4. Non-existent ID → 404 ProblemDetails
curl http://localhost:5002/api/contractors/99999

# 5. Paged list (page 2, 2 per page, sorted by capital desc)
curl "http://localhost:5002/api/contractors?page=2&pageSize=2&sortBy=capital&sortDir=desc"
# totalCount = total rows in DB, items = 2 (or fewer on last page)
```

### Architecture decisions (Lab 03)

**EF Core Code-First + Fluent API**: All entity configurations are in separate `IEntityTypeConfiguration<T>` classes under `Catalog.Dal/Configurations/`. `OnModelCreating` uses `ApplyConfigurationsFromAssembly` — one line.

**Two-phase seeding**: `WorkCategory` rows use `HasData` with fixed Ids (applied by migration, idempotent). `Contractor` rows use `DbInitializer.SeedAsync` with an `AnyAsync` guard (skipped on restart).

**Specification pattern**: `ContractorsFilterSpec` and `ContractorsCountSpec` (Ardalis.Specification) encapsulate filter/sort/page logic. The repository applies them via `WithSpecification(spec)` — the service only constructs and passes specs.

**FluentValidation**: Validators (`CreateContractorDtoValidator`, `UpdateContractorDtoValidator`) are registered via `AddValidatorsFromAssemblyContaining<>` and called explicitly in controllers — no deprecated `AddFluentValidation` middleware.

**Serilog SQL logging**: `MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)` enables EF Core SQL query logging — verify that Eager Loading logs 1 query and Explicit Loading logs 2.

---

## Lab 04 — Risk Assessments (Clean Architecture + CQRS + MongoDB)

Project №3 from Lab 01, now a microservice. Database `RiskAssessment`. Collections stay `riskAssessments`, `auditNotes`, `anomalyRules`, and `discussions`.

### Layer diagram

Dependencies point inward. `RiskAssessments.Domain` references no other project and does not reference `MongoDB.Driver`.

```
RiskAssessments.WebApi          controllers, Swagger, ProblemDetails, Serilog
        │  references
        ├── RiskAssessments.Application     commands, queries, validators, behaviors
        │         │  references
        │         └── RiskAssessments.Domain    entities, value objects, repository ports
        └── RiskAssessments.Infrastructure  MongoDbContext, BsonClassMap, repositories, seed, indexes
                  │  references
                  ├── RiskAssessments.Application
                  └── RiskAssessments.Domain
```

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `Services/RiskAssessments/RiskAssessments.Domain` | `RiskAssessment`, `TriggeredFlag`, `AuditNote`; value objects `Auditor`, `Money`, `Attachment`; `IRiskAssessmentRepository` |
| Application | `RiskAssessments.Application` | MediatR commands/queries, FluentValidation, `IRiskAssessmentQueries`, pipeline behaviors |
| Infrastructure | `RiskAssessments.Infrastructure` | MongoDB driver, camelCase `BsonClassMap`, optimistic version check, idempotent seed, indexes |
| Presentation | `RiskAssessments.WebApi` | Controllers talk only to `ISender`. AutoMapper maps `ObjectId` → `string` |

### Document model (same as Lab 01)

| Collection | Shape |
|------------|--------|
| `riskAssessments` | One document per tender. Embedded `triggeredFlags[]`. Referenced `auditNoteIds[]`. Snapshots `tenderTitle`, `procuringEntityName`. |
| `auditNotes` | Hybrid: embedded `smallAttachments[]`, optional `discussionId`. |
| `anomalyRules` | Flexible schema. `PRICE_DUMPING` has `maxPriceRatioPct`; `SHELL_COMPANY` has `maxCompanyAgeDays` and `maxAuthorizedCapital`; `SINGLE_BIDDER` has neither. |
| `discussions` | Referenced from a long note. Messages embedded. |

`version` is the optimistic-concurrency token. `GET` returns it as `ETag`. `PUT` and `DELETE` send it as `If-Match`.

Score bands: Low 0–29, Medium 30–54, High 55–79, Critical 80–100.

### Quickstart

```powershell
docker compose up -d mongodb
dotnet run --project Services\RiskAssessments\RiskAssessments.WebApi
```

Swagger: http://localhost:5004/swagger  
Health: http://localhost:5004/health  

Startup creates indexes and, outside Production, seeds only when a collection is empty. A second start does not insert duplicates.

`appsettings.json` / `appsettings.Development.json` use database `RiskAssessment`. Staging uses `RiskAssessmentStaging`. Production uses `RiskAssessmentProd`.

### API

| Method | URL | Success | Error |
|--------|-----|---------|-------|
| `GET` | `/api/risk-assessments?procuringEntityId=&riskLevel=&page=&pageSize=` | 200 | 400 |
| `GET` | `/api/risk-assessments/cursor?afterId=&pageSize=` | 200 | 400 |
| `GET` | `/api/risk-assessments/search?text=` | 200 | 400 |
| `GET` | `/api/risk-assessments/{id}` | 200 + `ETag` | 400, 404 |
| `POST` | `/api/risk-assessments` | 201 + `Location` with a string ObjectId | 400, 409 |
| `PUT` | `/api/risk-assessments/{id}` + `If-Match` | 200 | 400, 404, 409 |
| `DELETE` | `/api/risk-assessments/{id}` + `If-Match` | 204 | 400, 404, 409 |
| `POST` | `/api/risk-assessments/{id}/flags` + `If-Match` | 200 | 400, 404, 409 |
| `GET` | `/api/risk-assessments/{id}/notes` | 200 | 400 |
| `POST` | `/api/risk-assessments/{id}/notes` | 201 | 400, 404, 409 |

### Self-check

```powershell
# Score 120 is rejected by FluentValidation before the handler.
# The log line is: Validation failed for CreateRiskAssessmentCommand; handler was not invoked
curl -X POST http://localhost:5004/api/risk-assessments `
  -H "Content-Type: application/json" `
  -d '{"tenderId":9,"tenderTitle":"Міст","procuringEntityId":1,"procuringEntityName":"ОДА","auditorId":"a-1","auditorName":"Олег","overallRiskScore":120,"riskLevel":"Critical"}'

# Second assessment for tender 2 (already seeded) → 400 from the async validator, or 409 if the unique index wins
curl -X POST http://localhost:5004/api/risk-assessments `
  -H "Content-Type: application/json" `
  -d '{"tenderId":2,"tenderTitle":"Дубль","procuringEntityId":2,"procuringEntityName":"ОДА","auditorId":"a-1","auditorName":"Олег","overallRiskScore":40,"riskLevel":"Medium"}'

# Unknown id → 404. Garbage id → 400 (ObjectId rule).
curl http://localhost:5004/api/risk-assessments/507f1f77bcf86cd799439011
curl http://localhost:5004/api/risk-assessments/not-an-id

# Indexes (mongosh or Compass)
mongosh mongodb://localhost:27017/RiskAssessment --eval "db.riskAssessments.getIndexes()"
```

### Tests

```powershell
dotnet test Services\RiskAssessments\RiskAssessments.Tests
```

Covers a score of 120 (no database), value-object equality, and a BSON round-trip: camelCase, embedded `triggeredFlags`, no leaked `_triggeredFlags` field.

---

## Lab 05 — .NET Aspire Platform: Gateway, Aggregator, OpenTelemetry

This lab integrates all three microservices (Labs 02–04) into a unified .NET Aspire platform with centralized observability, an API Gateway, and an Aggregator service.

### Platform architecture

| Component | Role | Technology |
|-----------|------|------------|
| `Platform.AppHost` | Aspire orchestrator — starts all services and DB containers with one command | .NET Aspire AppHost SDK |
| `Platform.ServiceDefaults` | Shared library — Serilog, OpenTelemetry, CorrelationId, health checks, resilience | Serilog, OTel, Polly |
| `Platform.ApiGateway` | Single entry point for clients — routes requests via YARP | YARP + Aspire service discovery |
| `Platform.Aggregator` | Aggregator Pattern — parallel calls with partial failure tolerance | HttpClient + Task.WhenAll |
| `Tenders.Api` | Lab 02 microservice — tender lifecycle (ADO.NET + Dapper) | SQL Server (`ProcurementBidding`) |
| `Catalog.Api` | Lab 03 microservice — contractor registry (EF Core) | SQL Server (`ContractorRegistry`) |
| `RiskAssessments.WebApi` | Lab 04 microservice — risk assessment (Clean Architecture + CQRS) | MongoDB (`RiskAssessmentDb`) |

### Quick start (all-in-one with Aspire)

**Prerequisites:** Docker Desktop running, .NET 10 SDK, .NET Aspire workload installed.

```powershell
# Install Aspire workload (once)
dotnet workload install aspire

# Set the SQL Server password secret (once per machine)
cd Platform.AppHost
dotnet user-secrets set "sql-password" "YourStrong!Passw0rd"
cd ..

# Launch the entire platform
dotnet run --project Platform.AppHost
```

Aspire Dashboard opens automatically at `http://localhost:15888`. All services, SQL Server, and MongoDB containers start in the correct order.

**API Gateway** is available at `http://localhost:5000`.

### Gateway routes (via `http://localhost:5000`)

| Route pattern | Proxied to |
|---------------|-----------|
| `GET /api/tenders/{**}` | `tenders-api` |
| `GET /api/contractors/{**}` | `catalog-api` |
| `GET /api/risk-assessments/{**}` | `risk-assessments-api` |
| `GET /api/aggregator/{**}` | `aggregator` |

### Aggregator endpoint

```powershell
# Full tender view: tender data + contractor list + risk assessments
# (parallel calls with partial failure tolerance)
curl -i http://localhost:5000/api/aggregator/tenders/1/full

# Platform health aggregate across all services
curl -i http://localhost:5000/api/aggregator/health
```

**Response headers include `X-Correlation-Id`** — use it to find all logs for the request in Aspire Dashboard → Structured logs.

### Individual service endpoints (via Gateway)

```powershell
# Tenders
curl http://localhost:5000/api/tenders/1
curl http://localhost:5000/api/procuring-entities

# Contractors
curl "http://localhost:5000/api/contractors?page=1&pageSize=10"

# Risk Assessments
curl "http://localhost:5000/api/risk-assessments?page=1&pageSize=5"
```

### Observability (Aspire Dashboard)

1. **Traces** tab: search for the CorrelationId to find the full distributed trace. The aggregation trace shows spans for Gateway → Aggregator → (tenders-api, catalog-api, risk-assessments-api in parallel) → SQL/MongoDB queries.
2. **Structured logs** tab: filter by `CorrelationId` to find all log records from every service in one request chain. Each record includes `ServiceName`, `Environment`, `MachineName`, `ThreadId`.
3. **Metrics** tab: request counts, duration histograms, error rates per service.

### Partial failure scenario

Stop `risk-assessments-api` from the Aspire Dashboard, then:
```powershell
curl http://localhost:5000/api/aggregator/tenders/1/full
```
The response returns tender data + contractors with `warnings: ["risk-assessments-api is unavailable: ..."]` — HTTP 200, not 500.

### Standalone service startup (without Aspire)

Each service can still run independently with Docker containers:
```powershell
docker run -d -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
docker run -d -p 27017:27017 mongo:7

dotnet run --project Services\Tenders\Tenders.Api
dotnet run --project Services\Catalog\Catalog.Api
dotnet run --project Services\RiskAssessments\RiskAssessments.WebApi
```

---

## Lab 07 — Resilience, business metrics, and health checks

Temporary failures are retried. A dependency that keeps failing opens a circuit breaker and calls fail fast. Redis is optional: if it is down, each service reads its own database and `/health/ready` reports **Degraded**, not Unhealthy. Liveness does not touch databases.

### Resilience policies (Aggregator clients)

The standard handler is installed once, in `Platform.ServiceDefaults` (`ConfigureHttpClientDefaults`). Each Aggregator client calls `RemoveAllResilienceHandlers()` and then adds **one** custom handler, so pipelines are not stacked.

gRPC status `Unavailable` is HTTP 200 plus trailers, so the HTTP handler does not retry it. Every gRPC channel also has a `ServiceConfig` `RetryPolicy` for `Unavailable` and `ResourceExhausted`. `MaxAttempts` includes the original call.

`SamplingDuration` is at least twice `AttemptTimeout`. `MinimumThroughput` is lowered from the production default of 100 so a short failure burst on this stand can open the breaker.

| Client | Role | HTTP retries | Backoff | Attempt / total timeout | Circuit breaker | gRPC `MaxAttempts` | Why |
|--------|------|--------------|---------|-------------------------|-----------------|--------------------|-----|
| `catalog-api` | **Critical.** The dossier cannot name who bid, or explain a shell-company flag, without the contractor registry. | 5, jitter | Exponential, 200 ms base | 3 s / 30 s | Failure ratio **0.3**, minimum throughput **5**, window **30 s**, break **15 s**. Opens, half-opens, and closes are logged. | 5 | More retries and a sensitive breaker. Losing the registry hides collusion. |
| `tenders-api` | **Critical.** Without the tender there is nothing to audit. | 4, jitter | Exponential, 200 ms base | 3 s / 25 s | Failure ratio **0.25** (stricter than catalog), minimum throughput **5**, window **30 s**, break **20 s**. Logged transitions. | 4 | The tender is the spine of the dossier. Fail fast once the breaker has evidence, but retry brief blips. |
| `risk-assessments-api` | **Non-critical.** Flags enrich the dossier. The Aggregator already returns the tender and contractors with a warning. | 1, jitter | Exponential, 200 ms base | 2 s / 6 s | Failure ratio **0.7** (tolerant), minimum throughput **6**, window **20 s**, break **10 s**. Logged transitions. | 2 | Give up quickly and return a partial dossier instead of holding the auditor. |
| `procurement-commands` | **Writes** (publish a tender, award a bid). Not used by the read aggregator. | 3, jitter, **`DisableForUnsafeHttpMethods()`** | Exponential, 1 s base | 3 s / 20 s | Failure ratio 0.5, minimum throughput 5, window 30 s, break 15 s. | — | POST/PUT/DELETE are not retried, so a retried award cannot be applied twice. GET still retries. |

Read RPCs travel as HTTP POST, but they are idempotent queries, so the three read clients keep HTTP retries for connection failures. That is what produces `resilience.polly.strategy.events` with `event.name = OnRetry` when a service is stopped. gRPC `RetryPolicy` covers `Unavailable` on top of that.

### Business metrics

Meters are created with `IMeterFactory`, registered as singletons, and exported from ServiceDefaults (`AddMeter`, including `Polly`).

| Meter | Instruments | Tags (bounded) |
|-------|-------------|----------------|
| `Tenders.Api` | `tenders.created`, `tenders.status_changed`, `bids.submitted`, `tenders.awarded`, histogram `tenders.operation.duration`, gauges `tenders.awaiting_publication` and `tenders.operations.inflight` | `tender.status` (Draft, Published, BiddingOpen, EvaluationInProgress, Awarded, Cancelled, Other), `operation`, `status` = success \| failure |
| `Catalog.Api` | `contractors.registered`, `contractors.changed`, histogram `contractors.operation.duration`, gauges `contractors.active` and `contractors.operations.inflight` | `contractor.result` (registered, updated, removed, activated, deactivated) |
| `RiskAssessments.Api` | `risk_assessments.created`, `risk_assessments.updated`, histogram `risk_assessments.operation.duration`, gauges `risk_assessments.open` and `risk_assessments.operations.inflight` | `risk.level` (Low, Medium, High, Critical, Other) |
| `Platform.Aggregator` | `aggregator.dossiers`, histogram `aggregator.operation.duration`, gauge `aggregator.operations.inflight` | `result` = complete \| partial |
| `Polly` | `resilience.polly.strategy.events`, `resilience.polly.strategy.attempt.duration`, `resilience.polly.pipeline.duration` | `event.name` (OnRetry, OnCircuitOpened, OnCircuitHalfOpened, OnCircuitClosed, OnTimeout), `pipeline.name` |

Histograms record **every** execution, including failures. No tag carries a tender id, contractor id, EDRPOU, or auditor id.

Gauges are process-local snapshots (in-flight work, and drafts or assessments this process has created). They are not a cluster-wide count.

### What to watch in the Aspire Dashboard

Aspire Dashboard is a development tool. Production would use Grafana, Prometheus, or Azure Monitor. These are the signals worth keeping on screen while developing.

| Signal | Where | Healthy range on this stand | Filter when something looks wrong |
|--------|-------|-----------------------------|-----------------------------------|
| `tenders.created` | Metrics, meter `Tenders.Api` | Grows by 1 per created tender. Split by `tender.status`. | Flat while POSTs return 201 — meter name missing from ServiceDefaults. |
| `tenders.operation.duration` | Metrics | Most creates under a few hundred milliseconds against local SQL. | `status = failure` or a p95 that jumps after a retry policy change. |
| `contractors.active` / `risk_assessments.open` | Metrics | Move with register and delete calls in this process. | — |
| `aggregator.dossiers` | Metrics | `result = complete` while all three services are up. | `result = partial` means risk assessments (or another optional call) failed open. |
| `resilience.polly.strategy.events` | Metrics, meter `Polly` | Rare `OnRetry`. `OnCircuitOpened` should be absent in normal use. | `event.name = OnRetry` or `OnCircuitOpened`, then narrow by `pipeline.name`. |
| Structured logs | Logs | One chain per `CorrelationId`, property `ServiceName`. | Category `Polly` or message "Circuit opened". Level Error for open, Information for close. |
| Traces | Traces | Gateway → Aggregator → three services → SQL or Mongo. | Several spans with the same RPC name are retries. Compare duration before and after a policy change. |
| Resources | Resources | Green while `/health` returns 200. | Red when SQL or Mongo is down (`/health` is 503). Redis down stays green on `/health` because Degraded is HTTP 200; the JSON of `/health/ready` shows `redis-cache: Degraded`. |

### Health endpoints

Mapped in every environment by `MapDefaultEndpoints` (not only Development):

| Path | What it runs | SQL stopped | Redis stopped |
|------|----------------|-------------|---------------|
| `/health/live`, `/alive` | Tag `live` only (the process) | **200** | **200** |
| `/health/ready` | Tag `ready`. JSON lists each check. | **503**, body `Unhealthy` | **200**, body `Degraded`, check `redis-cache` is Degraded |
| `/health` | Every check. Aspire `WithHttpHealthCheck("/health")` polls this. `WaitFor` waits until it succeeds. | **503** | **200** (Degraded is still success for the probe) |

Checks:

| Check | Service | Failure status | Tags |
|-------|---------|----------------|------|
| `tenders-db` | Tenders.Api | Unhealthy | `database`, `sql`, `ready`. Query is `SELECT 1`. Timeout 5 s. |
| `catalog-db` | Catalog.Api | Unhealthy | `database`, `sql`, `ready`. Query is `SELECT 1`. Timeout 5 s. |
| `risk-assessments-db` | RiskAssessments.WebApi | Unhealthy | `database`, `mongodb`, `ready`. Ping. Timeout 5 s. |
| `redis-cache` | Each service and the Aggregator | **Degraded** | `cache`, `redis`, `ready`. `PING`. Timeout 3 s. |
| `tenders-api`, `catalog-api` | Aggregator, via `grpc.health.v1.Health` | Unhealthy | `grpc`, `downstream`, `ready`. Deadline 2 s. |
| `risk-assessments-api` | Aggregator | **Degraded** | same. A partial dossier is still served. |

Each gRPC server maps `MapGrpcHealthChecksService()`. `RpcException` on the Aggregator probe becomes Unhealthy and is logged.

### Run and check

```powershell
cd Platform.AppHost
dotnet user-secrets set "sql-password" "YourStrong!Passw0rd"
cd ..
dotnet run --project Platform.AppHost
```

Gateway: `http://localhost:5000`. Dashboard URL is printed at startup.

```powershell
# Health of a service that owns a database — use the tenders-api URL from the
# Dashboard, not the gateway. The gateway does not check SQL.
# Stop SQL: /health/live stays 200, /health/ready returns 503.
# Stop Redis: /health/ready stays 200 and the JSON body says Degraded on redis-cache.
curl http://localhost:<tenders-api-port>/health/live
curl http://localhost:<tenders-api-port>/health/ready

# Create a tender — tenders.created increases, tenders.operation.duration records the call.
curl -X POST http://localhost:5000/api/tenders `
  -H "Content-Type: application/json" `
  -d '{"procuringEntityId":1,"title":"School roof repair","expectedBudget":2500000,"description":"Roof","deliveryAddress":"Kyiv"}'

# Stop risk-assessments-api in the Dashboard for 5–10 s, then:
curl http://localhost:5000/api/aggregator/tenders/1/full
# HTTP 200, warnings mention risk-assessments-api, Polly metric OnRetry for that pipeline.
# A longer outage opens the breaker (log: "Circuit opened", metric OnCircuitOpened).
# After BreakDuration (10 s) a trial call runs (half-open) and the circuit closes if it succeeds.
```

Stopping Redis: `/health/ready` JSON shows `redis-cache` as `Degraded` and the APIs still respond from SQL Server or MongoDB. Stopping SQL Server: `/health/live` is 200 and `/health/ready` is 503.
