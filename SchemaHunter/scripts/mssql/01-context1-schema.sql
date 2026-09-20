-- =============================================================================
-- Context 1 — Procurement & Risk Engine
-- Schema DDL: database, schema, tables, constraints, indexes
-- Source: context1-procurement-dapper-guide.md  §2–3
-- =============================================================================

-- Required for filtered indexes and other advanced index features
SET QUOTED_IDENTIFIER ON;
GO

SET ANSI_NULLS ON;
GO

-- ─── §2  Database & Schema ───────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SchemaHunterProcurement')
BEGIN
    CREATE DATABASE SchemaHunterProcurement
        COLLATE Ukrainian_CI_AS;
END;
GO

USE SchemaHunterProcurement;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'procurement')
BEGIN
    EXEC('CREATE SCHEMA procurement');
END;
GO

-- ─── §3.1  risk_threshold_rules ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'procurement.risk_threshold_rules') AND type = 'U'
)
BEGIN
    CREATE TABLE procurement.risk_threshold_rules
    (
        rule_id         BIGINT IDENTITY(1,1)  NOT NULL,
        rule_name       NVARCHAR(200)         NOT NULL,
        description     NVARCHAR(1000)        NULL,
        -- Metric key: SingleBidder | BudgetDeviation | CompanyAge | SameAddress | RelatedParty
        metric          NVARCHAR(100)         NOT NULL,
        threshold_value DECIMAL(18,4)         NOT NULL,
        -- Low | Medium | High | Critical
        severity_level  NVARCHAR(20)          NOT NULL
            CONSTRAINT chk_rtl_severity CHECK (severity_level IN ('Low','Medium','High','Critical')),
        is_active       BIT                   NOT NULL CONSTRAINT df_rtl_is_active DEFAULT 1,
        created_at      DATETIME2(3)          NOT NULL CONSTRAINT df_rtl_created_at DEFAULT GETUTCDATE(),

        CONSTRAINT pk_risk_threshold_rules PRIMARY KEY CLUSTERED (rule_id),
        CONSTRAINT uq_rtl_rule_name        UNIQUE (rule_name)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_rtl_metric_active' AND object_id = OBJECT_ID('procurement.risk_threshold_rules'))
    CREATE INDEX ix_rtl_metric_active
        ON procurement.risk_threshold_rules (metric, is_active)
        WHERE is_active = 1;
GO

-- ─── §3.2  suppliers ─────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'procurement.suppliers') AND type = 'U'
)
BEGIN
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
        -- Computed risk score in range [0.00, 100.00]
        risk_score        DECIMAL(5,2)         NOT NULL CONSTRAINT df_sup_risk_score DEFAULT 0.00
            CONSTRAINT chk_sup_risk_score CHECK (risk_score BETWEEN 0 AND 100),
        is_deleted        BIT                  NOT NULL CONSTRAINT df_sup_is_deleted DEFAULT 0,
        created_at        DATETIME2(3)         NOT NULL CONSTRAINT df_sup_created_at DEFAULT GETUTCDATE(),

        CONSTRAINT pk_suppliers    PRIMARY KEY CLUSTERED (supplier_id),
        CONSTRAINT uq_sup_edrpou   UNIQUE (edrpou_code)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_sup_legal_name' AND object_id = OBJECT_ID('procurement.suppliers'))
    CREATE INDEX ix_sup_legal_name ON procurement.suppliers (legal_name) WHERE is_deleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_sup_risk_score' AND object_id = OBJECT_ID('procurement.suppliers'))
    CREATE INDEX ix_sup_risk_score ON procurement.suppliers (risk_score DESC) WHERE is_deleted = 0;
GO

-- ─── §3.3  tenders ───────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'procurement.tenders') AND type = 'U'
)
BEGIN
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
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_ten_status' AND object_id = OBJECT_ID('procurement.tenders'))
    CREATE INDEX ix_ten_status         ON procurement.tenders (status)       WHERE is_deleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_ten_project_code' AND object_id = OBJECT_ID('procurement.tenders'))
    CREATE INDEX ix_ten_project_code   ON procurement.tenders (project_code) WHERE project_code IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_ten_published_at' AND object_id = OBJECT_ID('procurement.tenders'))
    CREATE INDEX ix_ten_published_at   ON procurement.tenders (published_at DESC);
GO

-- ─── §3.4  tender_bids ───────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'procurement.tender_bids') AND type = 'U'
)
BEGIN
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
        CONSTRAINT fk_bid_supplier           FOREIGN KEY (supplier_id) REFERENCES procurement.suppliers (supplier_id) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_bid_supplier_id' AND object_id = OBJECT_ID('procurement.tender_bids'))
    CREATE INDEX ix_bid_supplier_id ON procurement.tender_bids (supplier_id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_bid_is_winner' AND object_id = OBJECT_ID('procurement.tender_bids'))
    CREATE INDEX ix_bid_is_winner   ON procurement.tender_bids (tender_id, is_winner) WHERE is_winner = 1;
GO

-- ─── §3.5  risk_assessments ──────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'procurement.risk_assessments') AND type = 'U'
)
BEGIN
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
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_ra_severity_level' AND object_id = OBJECT_ID('procurement.risk_assessments'))
    CREATE INDEX ix_ra_severity_level ON procurement.risk_assessments (severity_level);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_ra_overall_score' AND object_id = OBJECT_ID('procurement.risk_assessments'))
    CREATE INDEX ix_ra_overall_score  ON procurement.risk_assessments (overall_score DESC);
GO

PRINT 'Context 1 schema created successfully.';
GO
