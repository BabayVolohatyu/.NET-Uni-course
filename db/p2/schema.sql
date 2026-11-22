-- ============================================================
-- Project №2: Contractor Registry Context
-- Technology: SQL Server + EF Core (Code First, Fluent API)
-- Domain: Automated Anomaly Detection in Public Reconstruction Procurement
-- Context: Contractor Qualification & Registry (rich relational catalog)
-- ============================================================
-- Note: This DDL represents the schema that EF Core Code First will produce
-- via migrations in Lab 03.  Running it directly pre-creates the schema for
-- exploration and seed data loading in Lab 01.
-- ============================================================
-- Run order: schema.sql → seed.sql
-- ============================================================

USE master;
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ContractorRegistry')
    CREATE DATABASE ContractorRegistry
        COLLATE Cyrillic_General_CI_AS;
GO
USE ContractorRegistry;
GO

-- ============================================================
-- TABLE 1: Regions  (reference data — 1:N to Contractor via ContractorLicenses)
-- Ukrainian oblasts / regions where contractors are licensed to operate.
-- ============================================================
CREATE TABLE dbo.Regions (
    Id    INT IDENTITY(1,1) PRIMARY KEY,
    Name  NVARCHAR(100) NOT NULL,
    Code  NVARCHAR(10)  NOT NULL,
    CONSTRAINT UQ_Regions_Code UNIQUE (Code)
);
GO

-- ============================================================
-- TABLE 2: ServiceCategories  (reference data — M:N to Contractors)
-- CPV / DK021 procurement service categories (construction types).
-- ============================================================
CREATE TABLE dbo.ServiceCategories (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    Code        NVARCHAR(50)  NOT NULL,   -- CPV code, e.g. '45000000-7'
    Description NVARCHAR(500) NULL,
    ParentId    INT           NULL
        REFERENCES dbo.ServiceCategories(Id),   -- self-referential for category hierarchy
    CONSTRAINT UQ_ServiceCategories_Code UNIQUE (Code)
);
GO
-- Index: "look up categories by parent for hierarchy traversal"
CREATE INDEX IX_ServiceCategories_ParentId ON dbo.ServiceCategories (ParentId);
GO

-- ============================================================
-- TABLE 3: Contractors  (root entity of this context)
-- A private company or individual entrepreneur registered to perform reconstruction work.
-- ============================================================
CREATE TABLE dbo.Contractors (
    Id               BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name             NVARCHAR(300) NOT NULL,
    EDRPOU           CHAR(8)       NOT NULL,     -- Ukrainian business registry code
    LegalForm        NVARCHAR(50)  NOT NULL
        CONSTRAINT CK_Contractors_LegalForm
        CHECK (LegalForm IN (N'ТОВ', N'ПрАТ', N'АТ', N'ФОП', N'ДП', N'КП', N'Інше')),
    RegistrationDate DATE          NOT NULL,
    AuthorizedCapital DECIMAL(18,2) NOT NULL DEFAULT 0
        CONSTRAINT CK_Contractors_Capital CHECK (AuthorizedCapital >= 0),
    IsActive         BIT           NOT NULL DEFAULT 1,
    -- audit columns
    CreatedAt        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        NVARCHAR(100) NULL,
    UpdatedAt        DATETIME2     NULL,
    UpdatedBy        NVARCHAR(100) NULL,
    IsDeleted        BIT           NOT NULL DEFAULT 0,
    RowVersion       ROWVERSION,
    CONSTRAINT UQ_Contractors_EDRPOU UNIQUE (EDRPOU)
);
GO
-- Index: "search contractors by name prefix" — public search API
CREATE INDEX IX_Contractors_Name ON dbo.Contractors (Name)
    WHERE IsDeleted = 0 AND IsActive = 1;
GO
-- Index: "filter contractors by registration date range" — shell-company detection query
CREATE INDEX IX_Contractors_RegistrationDate ON dbo.Contractors (RegistrationDate)
    WHERE IsDeleted = 0;
GO

-- ============================================================
-- TABLE 4: ContractorProfiles  (1:1 with Contractors — FK is also PK)
-- Extended contact and description data; kept separate to keep Contractors table lean.
-- ============================================================
CREATE TABLE dbo.ContractorProfiles (
    ContractorId   BIGINT PRIMARY KEY     -- FK is PK → 1:1 relationship
        REFERENCES dbo.Contractors(Id) ON DELETE CASCADE,
    Website        NVARCHAR(500) NULL,
    ContactEmail   NVARCHAR(200) NULL,
    ContactPhone   NVARCHAR(30)  NULL,
    HeadquartersAddress NVARCHAR(500) NULL,
    Description    NVARCHAR(MAX) NULL,
    -- audit
    CreatedAt      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2     NULL
);
GO

-- ============================================================
-- TABLE 5: ContractorServiceCategories  (M:N junction — Contractors ↔ ServiceCategories)
-- A contractor can be certified in multiple service categories;
-- a category can have many certified contractors.
-- Junction table has its own fields: certification details (required for M:N with own field).
-- ============================================================
CREATE TABLE dbo.ContractorServiceCategories (
    ContractorId       BIGINT NOT NULL REFERENCES dbo.Contractors(Id) ON DELETE CASCADE,
    ServiceCategoryId  INT    NOT NULL REFERENCES dbo.ServiceCategories(Id),
    CertifiedAt        DATE   NOT NULL,                    -- own field on M:N — when certification was granted
    ExpiresAt          DATE   NULL,                        -- own field — expiry date (NULL = no expiry)
    CertificateNumber  NVARCHAR(100) NULL,                 -- own field — official certificate identifier
    PRIMARY KEY (ContractorId, ServiceCategoryId)
);
GO
-- Index: "find all contractors certified in a specific category" — procurement matching query
CREATE INDEX IX_ContractorServiceCategories_ServiceCategoryId
    ON dbo.ContractorServiceCategories (ServiceCategoryId, CertifiedAt);
GO
-- Index: "find certifications expiring soon for a contractor" — compliance alerting query
CREATE INDEX IX_ContractorServiceCategories_ExpiresAt
    ON dbo.ContractorServiceCategories (ExpiresAt)
    WHERE ExpiresAt IS NOT NULL;
GO

-- ============================================================
-- TABLE 6: ContractorLicenses  (1:N from Contractors, N:1 to Regions)
-- A contractor may hold construction licenses in multiple regions.
-- ============================================================
CREATE TABLE dbo.ContractorLicenses (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    ContractorId   BIGINT NOT NULL REFERENCES dbo.Contractors(Id) ON DELETE CASCADE,  -- 1:N left side
    RegionId       INT    NOT NULL REFERENCES dbo.Regions(Id),                        -- N:1 right side
    LicenseNumber  NVARCHAR(100) NOT NULL,
    IssuedAt       DATE          NOT NULL,
    ValidUntil     DATE          NULL,
    IsActive       BIT           NOT NULL DEFAULT 1,
    -- audit
    CreatedAt      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2     NULL,
    CONSTRAINT UQ_ContractorLicenses_Number UNIQUE (LicenseNumber)
);
GO
-- Index: "find all licenses for a contractor" — profile page query
CREATE INDEX IX_ContractorLicenses_ContractorId
    ON dbo.ContractorLicenses (ContractorId)
    WHERE IsActive = 1;
GO
-- Index: "find active licenses by region" — regional analytics query
CREATE INDEX IX_ContractorLicenses_RegionId
    ON dbo.ContractorLicenses (RegionId)
    WHERE IsActive = 1;
GO
