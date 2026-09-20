# Context 1 — Procurement & Risk Engine
## SQL Server + ADO.NET & Dapper Implementation Guide

**Bounded Context:** Procurement & Risk Engine  
**Persistence:** SQL Server — raw SQL via ADO.NET + Dapper (no ORM mapping layer)  
**Isolation rule:** Zero cross-database foreign keys. References to Context 2 assets are stored as plain `project_code VARCHAR(50)` (a cached duplicate of `reconstruction_projects.asset_code`).

---

## 1. Domain Overview

```
suppliers ──(1:N)──> tender_bids <──(N:1)── tenders
                                                │
                                               (1:1)
                                                │
                                        risk_assessments
                                                │
                                              (N:1)
                                                │
                                      risk_threshold_rules
```

| Table | PK | Relationship |
|---|---|---|
| `suppliers` | `supplier_id BIGINT IDENTITY` | 1:N `tender_bids` |
| `tenders` | `tender_id BIGINT IDENTITY` | 1:N `tender_bids`, 1:1 `risk_assessments` |
| `tender_bids` | `bid_id BIGINT IDENTITY` | M:N bridge between `suppliers` and `tenders` |
| `risk_assessments` | `risk_assessment_id BIGINT IDENTITY` | 1:1 dependent of `tenders` |
| `risk_threshold_rules` | `rule_id BIGINT IDENTITY` | lookup, 1:N `risk_assessments` |

---

## 2. DDL — Database & Schema

```sql
-- Create database (run once as sysadmin)
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SchemaHunterProcurement')
BEGIN
    CREATE DATABASE SchemaHunterProcurement
        COLLATE Ukrainian_CI_AS;
END;
GO

USE SchemaHunterProcurement;
GO

CREATE SCHEMA procurement;
GO
```

---

## 3. DDL — Table Definitions (3NF)

### 3.1 `procurement.risk_threshold_rules`
Lookup table — must be created before `risk_assessments` references it.

```sql
CREATE TABLE procurement.risk_threshold_rules
(
    rule_id        BIGINT IDENTITY(1,1)  NOT NULL,
    rule_name      NVARCHAR(200)         NOT NULL,
    description    NVARCHAR(1000)        NULL,
    -- Metric key: SingleBidder | BudgetDeviation | CompanyAge | SameAddress | RelatedParty
    metric         NVARCHAR(100)         NOT NULL,
    threshold_value DECIMAL(18,4)        NOT NULL,
    -- Low | Medium | High | Critical
    severity_level NVARCHAR(20)          NOT NULL
        CONSTRAINT chk_rtl_severity CHECK (severity_level IN ('Low','Medium','High','Critical')),
    is_active      BIT                   NOT NULL CONSTRAINT df_rtl_is_active DEFAULT 1,
    created_at     DATETIME2(3)          NOT NULL CONSTRAINT df_rtl_created_at DEFAULT GETUTCDATE(),

    CONSTRAINT pk_risk_threshold_rules PRIMARY KEY CLUSTERED (rule_id),
    CONSTRAINT uq_rtl_rule_name        UNIQUE (rule_name)
);
GO

CREATE INDEX ix_rtl_metric_active
    ON procurement.risk_threshold_rules (metric, is_active)
    WHERE is_active = 1;
GO
```

### 3.2 `procurement.suppliers`

```sql
CREATE TABLE procurement.suppliers
(
    supplier_id       BIGINT IDENTITY(1,1) NOT NULL,
    -- Ukrainian EDRPOU tax registration code (8–10 digits)
    edrpou_code       VARCHAR(10)          NOT NULL,
    legal_name        NVARCHAR(500)        NOT NULL,
    -- Active | Suspended | Bankrupt | Liquidated
    tax_status        NVARCHAR(50)         NOT NULL
        CONSTRAINT chk_sup_tax_status CHECK (tax_status IN ('Active','Suspended','Bankrupt','Liquidated')),
    registration_date DATE                 NOT NULL,
    -- Computed risk score in range [0.00, 100.00], updated by usp_SubmitBid / usp_ComputeRiskAssessment
    risk_score        DECIMAL(5,2)         NOT NULL CONSTRAINT df_sup_risk_score DEFAULT 0.00
        CONSTRAINT chk_sup_risk_score CHECK (risk_score BETWEEN 0 AND 100),
    is_deleted        BIT                  NOT NULL CONSTRAINT df_sup_is_deleted DEFAULT 0,
    created_at        DATETIME2(3)         NOT NULL CONSTRAINT df_sup_created_at DEFAULT GETUTCDATE(),

    CONSTRAINT pk_suppliers    PRIMARY KEY CLUSTERED (supplier_id),
    CONSTRAINT uq_sup_edrpou   UNIQUE (edrpou_code)
);
GO

CREATE INDEX ix_sup_legal_name ON procurement.suppliers (legal_name) WHERE is_deleted = 0;
CREATE INDEX ix_sup_risk_score ON procurement.suppliers (risk_score DESC) WHERE is_deleted = 0;
GO
```

### 3.3 `procurement.tenders`

```sql
CREATE TABLE procurement.tenders
(
    tender_id           BIGINT IDENTITY(1,1) NOT NULL,
    -- External ID from Prozorro API (e.g. UA-2024-01-15-000001-a)
    prozorro_id         VARCHAR(100)         NOT NULL,
    -- Cached duplicate of reconstruction_projects.asset_code (Context 2) — no FK
    project_code        VARCHAR(50)          NULL,
    title               NVARCHAR(1000)       NOT NULL,
    procuring_entity    NVARCHAR(500)        NOT NULL,
    expected_value      DECIMAL(18,2)        NOT NULL
        CONSTRAINT chk_ten_expected_value CHECK (expected_value > 0),
    currency            CHAR(3)              NOT NULL CONSTRAINT df_ten_currency DEFAULT 'UAH',
    -- Announced | Active | Cancelled | Awarded | Completed | Unsuccessful
    status              NVARCHAR(50)         NOT NULL CONSTRAINT df_ten_status DEFAULT 'Announced',
    published_at        DATETIME2(3)         NOT NULL,
    deadline_at         DATETIME2(3)         NULL,
    is_deleted          BIT                  NOT NULL CONSTRAINT df_ten_is_deleted DEFAULT 0,
    created_at          DATETIME2(3)         NOT NULL CONSTRAINT df_ten_created_at DEFAULT GETUTCDATE(),

    CONSTRAINT pk_tenders        PRIMARY KEY CLUSTERED (tender_id),
    CONSTRAINT uq_ten_prozorro   UNIQUE (prozorro_id)
);
GO

CREATE INDEX ix_ten_status         ON procurement.tenders (status)       WHERE is_deleted = 0;
CREATE INDEX ix_ten_project_code   ON procurement.tenders (project_code) WHERE project_code IS NOT NULL;
CREATE INDEX ix_ten_published_at   ON procurement.tenders (published_at DESC);
GO
```

### 3.4 `procurement.tender_bids`

```sql
CREATE TABLE procurement.tender_bids
(
    bid_id                  BIGINT IDENTITY(1,1) NOT NULL,
    tender_id               BIGINT               NOT NULL,
    supplier_id             BIGINT               NOT NULL,
    offered_amount          DECIMAL(18,2)        NOT NULL
        CONSTRAINT chk_bid_offered_amount CHECK (offered_amount > 0),
    submitted_at            DATETIME2(3)         NOT NULL,
    is_winner               BIT                  NOT NULL CONSTRAINT df_bid_is_winner  DEFAULT 0,
    -- Set when DQ'd — null means bid is active
    disqualification_reason NVARCHAR(500)        NULL,
    -- Optimistic concurrency token
    row_version             ROWVERSION           NOT NULL,

    CONSTRAINT pk_tender_bids            PRIMARY KEY CLUSTERED (bid_id),
    CONSTRAINT uq_bid_tender_supplier    UNIQUE (tender_id, supplier_id),
    CONSTRAINT fk_bid_tender             FOREIGN KEY (tender_id)   REFERENCES procurement.tenders   (tender_id)   ON DELETE CASCADE,
    CONSTRAINT fk_bid_supplier           FOREIGN KEY (supplier_id) REFERENCES procurement.suppliers (supplier_id) ON DELETE RESTRICT
);
GO

CREATE INDEX ix_bid_supplier_id ON procurement.tender_bids (supplier_id);
CREATE INDEX ix_bid_is_winner   ON procurement.tender_bids (tender_id, is_winner) WHERE is_winner = 1;
GO
```

### 3.5 `procurement.risk_assessments`

```sql
CREATE TABLE procurement.risk_assessments
(
    risk_assessment_id  BIGINT IDENTITY(1,1) NOT NULL,
    -- 1:1 with tenders — enforced by UNIQUE constraint
    tender_id           BIGINT               NOT NULL,
    -- Low | Medium | High | Critical
    severity_level      NVARCHAR(20)         NOT NULL
        CONSTRAINT chk_ra_severity CHECK (severity_level IN ('Low','Medium','High','Critical')),
    overall_score       DECIMAL(5,2)         NOT NULL
        CONSTRAINT chk_ra_score CHECK (overall_score BETWEEN 0 AND 100),
    -- JSON payload: [{ "ruleId": 3, "metric": "SingleBidder", "value": 1 }, ...]
    anomaly_flags       NVARCHAR(MAX)        NULL,
    assessed_at         DATETIME2(3)         NOT NULL,
    reviewed_by_rule_id BIGINT               NULL,
    notes               NVARCHAR(2000)       NULL,
    created_at          DATETIME2(3)         NOT NULL CONSTRAINT df_ra_created_at DEFAULT GETUTCDATE(),

    CONSTRAINT pk_risk_assessments       PRIMARY KEY CLUSTERED (risk_assessment_id),
    CONSTRAINT uq_ra_tender_id           UNIQUE (tender_id),
    CONSTRAINT fk_ra_tender              FOREIGN KEY (tender_id)           REFERENCES procurement.tenders              (tender_id) ON DELETE CASCADE,
    CONSTRAINT fk_ra_reviewed_by_rule    FOREIGN KEY (reviewed_by_rule_id) REFERENCES procurement.risk_threshold_rules (rule_id)   ON DELETE SET NULL
);
GO

CREATE INDEX ix_ra_severity_level ON procurement.risk_assessments (severity_level);
CREATE INDEX ix_ra_overall_score  ON procurement.risk_assessments (overall_score DESC);
GO
```

---

## 4. Stored Procedures

### 4.1 `usp_UpsertTender` — Insert or update tender by Prozorro ID

```sql
CREATE OR ALTER PROCEDURE procurement.usp_UpsertTender
    @ProzorroId         VARCHAR(100),
    @ProjectCode        VARCHAR(50)    = NULL,
    @Title              NVARCHAR(1000),
    @ProcuringEntity    NVARCHAR(500),
    @ExpectedValue      DECIMAL(18,2),
    @Currency           CHAR(3)        = 'UAH',
    @Status             NVARCHAR(50)   = 'Announced',
    @PublishedAt        DATETIME2(3),
    @DeadlineAt         DATETIME2(3)   = NULL,
    @TenderId           BIGINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM procurement.tenders WHERE prozorro_id = @ProzorroId AND is_deleted = 0)
    BEGIN
        UPDATE procurement.tenders
        SET
            project_code     = COALESCE(@ProjectCode, project_code),
            title            = @Title,
            procuring_entity = @ProcuringEntity,
            expected_value   = @ExpectedValue,
            currency         = @Currency,
            status           = @Status,
            deadline_at      = @DeadlineAt
        WHERE prozorro_id = @ProzorroId;

        SELECT @TenderId = tender_id
        FROM   procurement.tenders
        WHERE  prozorro_id = @ProzorroId;
    END
    ELSE
    BEGIN
        INSERT INTO procurement.tenders
            (prozorro_id, project_code, title, procuring_entity, expected_value, currency, status, published_at, deadline_at)
        VALUES
            (@ProzorroId, @ProjectCode, @Title, @ProcuringEntity, @ExpectedValue, @Currency, @Status, @PublishedAt, @DeadlineAt);

        SET @TenderId = SCOPE_IDENTITY();
    END
END;
GO
```

### 4.2 `usp_SubmitBid` — Register a bid and recalculate supplier risk score

```sql
CREATE OR ALTER PROCEDURE procurement.usp_SubmitBid
    @TenderId      BIGINT,
    @SupplierId    BIGINT,
    @OfferedAmount DECIMAL(18,2),
    @SubmittedAt   DATETIME2(3),
    @BidId         BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Validate tender exists and is active
    IF NOT EXISTS (SELECT 1 FROM procurement.tenders WHERE tender_id = @TenderId AND is_deleted = 0)
        THROW 50001, 'Tender not found or deleted.', 1;

    IF NOT EXISTS (SELECT 1 FROM procurement.suppliers WHERE supplier_id = @SupplierId AND is_deleted = 0)
        THROW 50002, 'Supplier not found or deleted.', 1;

    BEGIN TRANSACTION;

    -- UNIQUE(tender_id, supplier_id) enforced by constraint — duplicate will raise error naturally
    INSERT INTO procurement.tender_bids (tender_id, supplier_id, offered_amount, submitted_at)
    VALUES (@TenderId, @SupplierId, @OfferedAmount, @SubmittedAt);

    SET @BidId = SCOPE_IDENTITY();

    -- Recompute supplier risk_score:
    -- penalise if offered_amount deviates >30% below avg for this tender (possible collusion/dumping signal)
    DECLARE @AvgAmount DECIMAL(18,2);
    SELECT @AvgAmount = AVG(offered_amount)
    FROM   procurement.tender_bids
    WHERE  tender_id = @TenderId;

    IF @OfferedAmount < @AvgAmount * 0.70
    BEGIN
        UPDATE procurement.suppliers
        SET    risk_score = CASE WHEN risk_score + 5 > 100 THEN 100 ELSE risk_score + 5 END
        WHERE  supplier_id = @SupplierId;
    END

    COMMIT TRANSACTION;
END;
GO
```

### 4.3 `usp_GetTendersPaged` — Keyset-paginated tender list with filters

```sql
CREATE OR ALTER PROCEDURE procurement.usp_GetTendersPaged
    @Status          NVARCHAR(50)  = NULL,
    @ProjectCode     VARCHAR(50)   = NULL,
    @AfterTenderId   BIGINT        = 0,      -- last seen ID for keyset pagination
    @PageSize        INT           = 20
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@PageSize)
        t.tender_id,
        t.prozorro_id,
        t.project_code,
        t.title,
        t.procuring_entity,
        t.expected_value,
        t.currency,
        t.status,
        t.published_at,
        t.deadline_at,
        ra.severity_level   AS risk_severity,
        ra.overall_score    AS risk_score
    FROM procurement.tenders AS t
    LEFT JOIN procurement.risk_assessments AS ra ON ra.tender_id = t.tender_id
    WHERE
        t.is_deleted = 0
        AND t.tender_id > @AfterTenderId
        AND (@Status      IS NULL OR t.status       = @Status)
        AND (@ProjectCode IS NULL OR t.project_code = @ProjectCode)
    ORDER BY t.tender_id ASC;
END;
GO
```

### 4.4 `usp_SoftDeleteSupplier` — Soft delete supplier and withdraw all open bids

```sql
CREATE OR ALTER PROCEDURE procurement.usp_SoftDeleteSupplier
    @SupplierId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM procurement.suppliers WHERE supplier_id = @SupplierId AND is_deleted = 0)
        THROW 50003, 'Supplier not found or already deleted.', 1;

    BEGIN TRANSACTION;

    -- Mark all non-winning, non-disqualified bids as withdrawn
    UPDATE procurement.tender_bids
    SET    disqualification_reason = 'Supplier account deactivated'
    WHERE  supplier_id  = @SupplierId
      AND  is_winner    = 0
      AND  disqualification_reason IS NULL;

    UPDATE procurement.suppliers
    SET    is_deleted = 1
    WHERE  supplier_id = @SupplierId;

    COMMIT TRANSACTION;
END;
GO
```

### 4.5 `usp_ComputeRiskAssessment` — Evaluate rules and write risk assessment for a tender

```sql
CREATE OR ALTER PROCEDURE procurement.usp_ComputeRiskAssessment
    @TenderId            BIGINT,
    @RiskAssessmentId    BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @BidCount        INT;
    DECLARE @WinnerAmount    DECIMAL(18,2);
    DECLARE @AvgAmount       DECIMAL(18,2);
    DECLARE @ExpectedValue   DECIMAL(18,2);
    DECLARE @Score           DECIMAL(5,2) = 0;
    DECLARE @Severity        NVARCHAR(20) = 'Low';
    DECLARE @Flags           NVARCHAR(MAX) = '[]';
    DECLARE @RuleId          BIGINT       = NULL;

    SELECT
        @ExpectedValue = expected_value
    FROM procurement.tenders
    WHERE tender_id = @TenderId AND is_deleted = 0;

    IF @ExpectedValue IS NULL
        THROW 50004, 'Tender not found.', 1;

    SELECT
        @BidCount     = COUNT(*),
        @AvgAmount    = AVG(offered_amount),
        @WinnerAmount = MAX(CASE WHEN is_winner = 1 THEN offered_amount END)
    FROM procurement.tender_bids
    WHERE tender_id = @TenderId;

    -- Rule: SingleBidder — only one bid submitted
    IF @BidCount <= 1
    BEGIN
        SET @Score = @Score + 30;
        SELECT @RuleId = rule_id FROM procurement.risk_threshold_rules
        WHERE  metric = 'SingleBidder' AND is_active = 1;
        SET @Flags = JSON_MODIFY(@Flags, 'append $', JSON_OBJECT('metric':'SingleBidder','bid_count':@BidCount));
    END

    -- Rule: BudgetDeviation — winner price close to expected (possible leakage)
    IF @WinnerAmount IS NOT NULL AND @ExpectedValue > 0
    BEGIN
        DECLARE @Deviation DECIMAL(5,4) = ABS(@WinnerAmount - @ExpectedValue) / @ExpectedValue;
        IF @Deviation < 0.02   -- within 2% of budget ceiling
        BEGIN
            SET @Score = @Score + 40;
            SET @Flags = JSON_MODIFY(@Flags, 'append $',
                JSON_OBJECT('metric':'BudgetDeviation','deviation_pct':CAST(@Deviation * 100 AS DECIMAL(5,2))));
        END
    END

    -- Map score to severity
    SET @Severity = CASE
        WHEN @Score >= 70 THEN 'Critical'
        WHEN @Score >= 45 THEN 'High'
        WHEN @Score >= 20 THEN 'Medium'
        ELSE                   'Low'
    END;

    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM procurement.risk_assessments WHERE tender_id = @TenderId)
    BEGIN
        UPDATE procurement.risk_assessments
        SET
            severity_level      = @Severity,
            overall_score       = @Score,
            anomaly_flags       = @Flags,
            assessed_at         = GETUTCDATE(),
            reviewed_by_rule_id = @RuleId
        WHERE tender_id = @TenderId;

        SELECT @RiskAssessmentId = risk_assessment_id
        FROM   procurement.risk_assessments
        WHERE  tender_id = @TenderId;
    END
    ELSE
    BEGIN
        INSERT INTO procurement.risk_assessments
            (tender_id, severity_level, overall_score, anomaly_flags, assessed_at, reviewed_by_rule_id)
        VALUES
            (@TenderId, @Severity, @Score, @Flags, GETUTCDATE(), @RuleId);

        SET @RiskAssessmentId = SCOPE_IDENTITY();
    END

    COMMIT TRANSACTION;
END;
GO
```

### 4.6 `usp_GetRiskReportByProject` — Aggregated risk summary per project code

```sql
CREATE OR ALTER PROCEDURE procurement.usp_GetRiskReportByProject
    @ProjectCode VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- Summary row
    SELECT
        t.project_code,
        COUNT(DISTINCT t.tender_id)                                    AS tender_count,
        SUM(t.expected_value)                                          AS total_expected_budget,
        AVG(ra.overall_score)                                          AS avg_risk_score,
        MAX(ra.overall_score)                                          AS max_risk_score,
        SUM(CASE WHEN ra.severity_level = 'Critical' THEN 1 ELSE 0 END) AS critical_count,
        SUM(CASE WHEN ra.severity_level = 'High'     THEN 1 ELSE 0 END) AS high_count,
        SUM(CASE WHEN ra.severity_level = 'Medium'   THEN 1 ELSE 0 END) AS medium_count,
        SUM(CASE WHEN ra.severity_level = 'Low'      THEN 1 ELSE 0 END) AS low_count
    FROM procurement.tenders AS t
    INNER JOIN procurement.risk_assessments AS ra ON ra.tender_id = t.tender_id
    WHERE t.project_code = @ProjectCode
      AND t.is_deleted   = 0
    GROUP BY t.project_code;

    -- Tender detail rows
    SELECT
        t.tender_id,
        t.prozorro_id,
        t.title,
        t.expected_value,
        t.status,
        ra.severity_level,
        ra.overall_score,
        ra.assessed_at
    FROM procurement.tenders AS t
    INNER JOIN procurement.risk_assessments AS ra ON ra.tender_id = t.tender_id
    WHERE t.project_code = @ProjectCode
      AND t.is_deleted   = 0
    ORDER BY ra.overall_score DESC;
END;
GO
```

---

## 5. ADO.NET + Dapper Usage Patterns

### 5.1 Project Setup

```xml
<!-- Add to your .csproj -->
<PackageReference Include="Dapper"                   Version="2.*" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" />
```

### 5.2 Connection Factory

```csharp
using Microsoft.Data.SqlClient;

public class ProcurementConnectionFactory
{
    private readonly string _connectionString;

    public ProcurementConnectionFactory(string connectionString)
        => _connectionString = connectionString;

    public SqlConnection Create() => new SqlConnection(_connectionString);
}
```

### 5.3 Query DTOs

```csharp
public record TenderPageItem(
    long   TenderId,
    string ProzorroId,
    string? ProjectCode,
    string Title,
    string ProcuringEntity,
    decimal ExpectedValue,
    string Currency,
    string Status,
    DateTime PublishedAt,
    DateTime? DeadlineAt,
    string? RiskSeverity,
    decimal? RiskScore);

public record RiskReportSummary(
    string ProjectCode,
    int    TenderCount,
    decimal TotalExpectedBudget,
    decimal? AvgRiskScore,
    decimal? MaxRiskScore,
    int    CriticalCount,
    int    HighCount,
    int    MediumCount,
    int    LowCount);
```

### 5.4 Upsert Tender (stored procedure call)

```csharp
using Dapper;

public class TenderRepository
{
    private readonly ProcurementConnectionFactory _factory;

    public TenderRepository(ProcurementConnectionFactory factory)
        => _factory = factory;

    public async Task<long> UpsertAsync(UpsertTenderCommand cmd, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        var parameters = new DynamicParameters();
        parameters.Add("@ProzorroId",      cmd.ProzorroId);
        parameters.Add("@ProjectCode",     cmd.ProjectCode);
        parameters.Add("@Title",           cmd.Title);
        parameters.Add("@ProcuringEntity", cmd.ProcuringEntity);
        parameters.Add("@ExpectedValue",   cmd.ExpectedValue);
        parameters.Add("@Currency",        cmd.Currency);
        parameters.Add("@Status",          cmd.Status);
        parameters.Add("@PublishedAt",     cmd.PublishedAt);
        parameters.Add("@DeadlineAt",      cmd.DeadlineAt);
        parameters.Add("@TenderId",        dbType: DbType.Int64, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(
            "procurement.usp_UpsertTender",
            parameters,
            commandType: CommandType.StoredProcedure);

        return parameters.Get<long>("@TenderId");
    }
}
```

### 5.5 Paged Tender List

```csharp
public async Task<IReadOnlyList<TenderPageItem>> GetPagedAsync(
    string? status,
    string? projectCode,
    long    afterTenderId,
    int     pageSize,
    CancellationToken ct = default)
{
    await using var conn = _factory.Create();

    var result = await conn.QueryAsync<TenderPageItem>(
        "procurement.usp_GetTendersPaged",
        new { Status = status, ProjectCode = projectCode, AfterTenderId = afterTenderId, PageSize = pageSize },
        commandType: CommandType.StoredProcedure);

    return result.AsList();
}
```

### 5.6 Risk Report (multiple result sets)

```csharp
public async Task<(RiskReportSummary Summary, IReadOnlyList<RiskTenderRow> Tenders)>
    GetRiskReportAsync(string projectCode, CancellationToken ct = default)
{
    await using var conn = _factory.Create();

    using var multi = await conn.QueryMultipleAsync(
        "procurement.usp_GetRiskReportByProject",
        new { ProjectCode = projectCode },
        commandType: CommandType.StoredProcedure);

    var summary = await multi.ReadSingleAsync<RiskReportSummary>();
    var tenders = (await multi.ReadAsync<RiskTenderRow>()).AsList();

    return (summary, tenders);
}
```

### 5.7 Optimistic Concurrency — Reading RowVersion

```csharp
// tender_bids has a ROWVERSION column — read it back after insert
var bid = await conn.QuerySingleAsync<TenderBidRow>(
    "SELECT bid_id, row_version FROM procurement.tender_bids WHERE bid_id = @BidId",
    new { BidId = bidId });
```

---

## 6. Indexing Strategy Summary

| Table | Index | Type | Rationale |
|---|---|---|---|
| `suppliers` | `ix_sup_edrpou` (from UNIQUE) | B-tree | Lookup by tax code |
| `suppliers` | `ix_sup_risk_score` | Filtered DESC | Risk dashboard sort |
| `tenders` | `ix_ten_status` | Filtered | Status filter in paginated queries |
| `tenders` | `ix_ten_project_code` | Filtered | Cross-context join by project |
| `tenders` | `ix_ten_published_at` | B-tree DESC | Chronological listing |
| `tender_bids` | `ix_bid_supplier_id` | B-tree | Bids per supplier lookup |
| `tender_bids` | `ix_bid_is_winner` | Filtered covering | Winning bid fast path |
| `risk_assessments` | `ix_ra_severity_level` | B-tree | Filter by severity |
| `risk_assessments` | `ix_ra_overall_score` | B-tree DESC | Risk leaderboard |
| `risk_threshold_rules` | `ix_rtl_metric_active` | Filtered | Rule evaluation |

---

## 7. Eventual Consistency — Event Contracts

When `ReconstructionProject.AssetCode` is renamed in **Context 2**, that service publishes a domain event:

```json
{
  "eventType": "ProjectAssetCodeRenamed",
  "occurredAt": "2024-03-15T12:00:00Z",
  "payload": {
    "oldAssetCode": "ROAD-UA-001",
    "newAssetCode": "ROAD-UA-001-REV"
  }
}
```

The Procurement context subscribes and executes:

```sql
UPDATE procurement.tenders
SET    project_code = @NewAssetCode
WHERE  project_code = @OldAssetCode;
```

This keeps the cached duplicate eventually consistent without cross-database FKs.
