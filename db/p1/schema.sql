-- ============================================================
-- Project №1: Bidding / Procurement Process Context
-- Technology: SQL Server + ADO.NET & Dapper
-- Domain: Automated Anomaly Detection in Public Reconstruction Procurement
-- Context: Tender Submission & Bid Management (transactional, status-driven)
-- ============================================================
-- Run order: schema.sql → procedures.sql → seed.sql
-- ============================================================

USE master;
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ProcurementBidding')
    CREATE DATABASE ProcurementBidding
        COLLATE Cyrillic_General_CI_AS;
GO
USE ProcurementBidding;
GO

-- ============================================================
-- TABLE 1: ProcuringEntities  (Customer analog → 1:N to Tenders)
-- A state agency, city council, or municipality that publishes tenders.
-- ============================================================
CREATE TABLE dbo.ProcuringEntities (
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name          NVARCHAR(300) NOT NULL,
    EDRPOU        CHAR(8)       NOT NULL,           -- Ukrainian business-registry code (always 8 digits)
    Region        NVARCHAR(100) NOT NULL,
    ContactEmail  NVARCHAR(200) NULL,
    -- audit columns (Step 6)
    CreatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     NVARCHAR(100) NULL,
    UpdatedAt     DATETIME2     NULL,
    UpdatedBy     NVARCHAR(100) NULL,
    IsDeleted     BIT           NOT NULL DEFAULT 0,
    RowVersion    ROWVERSION,                       -- optimistic concurrency: prevents silent overwrites
    CONSTRAINT UQ_ProcuringEntities_EDRPOU UNIQUE (EDRPOU)
);
GO

-- ============================================================
-- TABLE 2: Tenders  (Order analog — has a status machine)
-- A public call for bids for reconstruction work.
-- ============================================================
CREATE TABLE dbo.Tenders (
    Id                   BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProcuringEntityId    BIGINT        NOT NULL
        REFERENCES dbo.ProcuringEntities(Id),       -- 1:N: one entity has many tenders
    Title                NVARCHAR(500) NOT NULL,
    ExpectedBudget       DECIMAL(18,2) NOT NULL
        CONSTRAINT CK_Tenders_ExpectedBudget CHECK (ExpectedBudget > 0),
    Status               NVARCHAR(30)  NOT NULL DEFAULT N'Draft'
        CONSTRAINT CK_Tenders_Status CHECK (Status IN (
            N'Draft', N'Published', N'BiddingOpen',
            N'EvaluationInProgress', N'Awarded', N'Cancelled')),
    PublishedAt          DATETIME2     NULL,
    BiddingDeadline      DATETIME2     NULL,
    -- audit columns
    CreatedAt            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy            NVARCHAR(100) NULL,
    UpdatedAt            DATETIME2     NULL,
    UpdatedBy            NVARCHAR(100) NULL,
    IsDeleted            BIT           NOT NULL DEFAULT 0,
    RowVersion           ROWVERSION
);
GO
-- Index: "all active tenders for a given procuring entity" — most common dashboard query
CREATE INDEX IX_Tenders_ProcuringEntityId_Status
    ON dbo.Tenders (ProcuringEntityId, Status)
    WHERE IsDeleted = 0;
GO
-- Index: "tenders whose bidding deadline is approaching" — scheduled alerting query
CREATE INDEX IX_Tenders_BiddingDeadline
    ON dbo.Tenders (BiddingDeadline)
    WHERE Status = N'BiddingOpen' AND IsDeleted = 0;
GO

-- ============================================================
-- TABLE 3: TenderDetails  (1:1 with Tenders — FK is also PK)
-- Extended description lives in a separate row; avoids wide Tenders table.
-- ============================================================
CREATE TABLE dbo.TenderDetails (
    TenderId            BIGINT PRIMARY KEY              -- FK is PK → 1:1 relationship
        REFERENCES dbo.Tenders(Id) ON DELETE CASCADE,
    Description         NVARCHAR(MAX) NOT NULL,
    CategoryCode        NVARCHAR(50)  NULL,             -- CPV / DK021 procurement category code
    DeliveryAddress     NVARCHAR(500) NOT NULL,
    TechnicalSpecUrl    NVARCHAR(1000) NULL,
    -- audit
    CreatedAt           DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2     NULL
);
GO

-- ============================================================
-- TABLE 4: Contractors  (local read-model snapshot from Registry context)
-- ContractorId matches the Id in the Contractor Registry DB — plain column, NO FK to other DB.
-- Name & EDRPOU are denormalized snapshot fields; kept up-to-date via domain events from Registry.
-- ============================================================
CREATE TABLE dbo.Contractors (
    Id              BIGINT PRIMARY KEY,                 -- same Id as Registry DB; no FOREIGN KEY constraint
    Name            NVARCHAR(300) NOT NULL,             -- denormalized from Registry (updated by events)
    EDRPOU          CHAR(8)       NOT NULL,             -- denormalized from Registry (updated by events)
    -- audit
    CreatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2     NULL
);
GO
-- Index: "look up local contractor snapshot by EDRPOU when receiving Registry events"
CREATE INDEX IX_Contractors_EDRPOU ON dbo.Contractors (EDRPOU);
GO

-- ============================================================
-- TABLE 5: Bids  (M:N between Tenders and Contractors — like OrderItems)
-- A contractor's price proposal for a specific tender.
-- ContractorName & ContractorEDRPOU are SNAPSHOTS fixed at submission time (never updated).
-- ============================================================
CREATE TABLE dbo.Bids (
    TenderId          BIGINT        NOT NULL
        REFERENCES dbo.Tenders(Id) ON DELETE CASCADE,  -- M:N left FK
    ContractorId      BIGINT        NOT NULL
        REFERENCES dbo.Contractors(Id),                -- M:N right FK
    ContractorName    NVARCHAR(300) NOT NULL,           -- snapshot at bid time — not updated (duplication policy)
    ContractorEDRPOU  CHAR(8)       NOT NULL,           -- snapshot at bid time — not updated
    ProposedPrice     DECIMAL(18,2) NOT NULL
        CONSTRAINT CK_Bids_ProposedPrice CHECK (ProposedPrice > 0),
    PriceReductionPct DECIMAL(5,2)  NOT NULL DEFAULT 0
        CONSTRAINT CK_Bids_Reduction CHECK (PriceReductionPct >= 0 AND PriceReductionPct < 100),
    SubmittedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    IsWinner          BIT           NOT NULL DEFAULT 0,
    IsDisqualified    BIT           NOT NULL DEFAULT 0,
    -- audit columns
    CreatedAt         DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy         NVARCHAR(100) NULL,
    UpdatedAt         DATETIME2     NULL,
    UpdatedBy         NVARCHAR(100) NULL,
    IsDeleted         BIT           NOT NULL DEFAULT 0,
    RowVersion        ROWVERSION,
    PRIMARY KEY (TenderId, ContractorId)                -- composite PK — M:N bridge table
);
GO
-- Index: "rank bids by proposed price for evaluation committee" — primary evaluation query
CREATE INDEX IX_Bids_TenderId_ProposedPrice
    ON dbo.Bids (TenderId, ProposedPrice ASC)
    WHERE IsDeleted = 0 AND IsDisqualified = 0;
GO
-- Index: "all tenders a contractor has participated in" — contractor analytics / anomaly detection
CREATE INDEX IX_Bids_ContractorId
    ON dbo.Bids (ContractorId)
    WHERE IsDeleted = 0;
GO

-- ============================================================
-- TABLE 6: TenderStatusHistory  (1:N from Tenders — audit log of status transitions)
-- Records every status change for compliance and anomaly-detection history.
-- ============================================================
CREATE TABLE dbo.TenderStatusHistory (
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    TenderId      BIGINT        NOT NULL
        REFERENCES dbo.Tenders(Id) ON DELETE CASCADE,  -- 1:N: one tender has many history entries
    FromStatus    NVARCHAR(30)  NOT NULL,
    ToStatus      NVARCHAR(30)  NOT NULL,
    ChangedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    ChangedBy     NVARCHAR(100) NULL,
    Reason        NVARCHAR(500) NULL
);
GO
-- Index: "retrieve status history for a tender in chronological order" — audit trail query
CREATE INDEX IX_TenderStatusHistory_TenderId_ChangedAt
    ON dbo.TenderStatusHistory (TenderId, ChangedAt DESC);
GO
