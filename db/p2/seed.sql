-- ============================================================
-- Project №2: Seed Data — Contractor Registry Context
-- SQL Server + EF Core
-- Run order: schema.sql → seed.sql
-- IDEMPOTENT: safe to run multiple times — uses IF NOT EXISTS before every insert.
-- ============================================================

USE ContractorRegistry;
GO

-- ============================================================
-- Seed: Regions (reference data)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Code = N'UA-32')
    INSERT INTO dbo.Regions (Name, Code) VALUES (N'Київська область',       N'UA-32');
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Code = N'UA-63')
    INSERT INTO dbo.Regions (Name, Code) VALUES (N'Харківська область',      N'UA-63');
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Code = N'UA-48')
    INSERT INTO dbo.Regions (Name, Code) VALUES (N'Миколаївська область',    N'UA-48');
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Code = N'UA-26')
    INSERT INTO dbo.Regions (Name, Code) VALUES (N'Івано-Франківська область', N'UA-26');
IF NOT EXISTS (SELECT 1 FROM dbo.Regions WHERE Code = N'UA-71')
    INSERT INTO dbo.Regions (Name, Code) VALUES (N'Черкаська область',       N'UA-71');
GO

-- ============================================================
-- Seed: ServiceCategories (reference — CPV codes for construction)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE Code = N'45000000-7')
    INSERT INTO dbo.ServiceCategories (Name, Code, Description, ParentId)
    VALUES (N'Будівельні роботи', N'45000000-7', N'Загальні будівельні та монтажні роботи', NULL);
GO

DECLARE @ParentId INT = (SELECT Id FROM dbo.ServiceCategories WHERE Code = N'45000000-7');

IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE Code = N'45200000-9')
    INSERT INTO dbo.ServiceCategories (Name, Code, Description, ParentId)
    VALUES (N'Будівельні роботи для цивільного та промислового будівництва', N'45200000-9', NULL, @ParentId);

IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE Code = N'45220000-5')
    INSERT INTO dbo.ServiceCategories (Name, Code, Description, ParentId)
    VALUES (N'Роботи з будівництва інженерних споруд', N'45220000-5', NULL, @ParentId);

IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE Code = N'45260000-7')
    INSERT INTO dbo.ServiceCategories (Name, Code, Description, ParentId)
    VALUES (N'Покрівельні роботи та суміжні роботи', N'45260000-7', NULL, @ParentId);

IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE Code = N'45330000-9')
    INSERT INTO dbo.ServiceCategories (Name, Code, Description, ParentId)
    VALUES (N'Санітарно-технічні роботи', N'45330000-9', NULL, @ParentId);
GO

-- ============================================================
-- Seed: Contractors
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Contractors WHERE EDRPOU = '31415926')
BEGIN
    INSERT INTO dbo.Contractors (Name, EDRPOU, LegalForm, RegistrationDate, AuthorizedCapital, IsActive, CreatedBy)
    VALUES (N'ТОВ «БудМайстер»', '31415926', N'ТОВ', '2010-03-15', 500000.00, 1, N'seed');

    DECLARE @C1Id BIGINT = SCOPE_IDENTITY();

    INSERT INTO dbo.ContractorProfiles (ContractorId, Website, ContactEmail, ContactPhone, HeadquartersAddress, Description)
    VALUES (@C1Id, N'https://budmaister.ua', N'info@budmaister.ua', N'+380441234567',
            N'м. Київ, вул. Будівельників, 10', N'Провідна будівельна компанія з досвідом відновлення шкіл та лікарень.');

    -- Certifications (M:N)
    INSERT INTO dbo.ContractorServiceCategories (ContractorId, ServiceCategoryId, CertifiedAt, ExpiresAt, CertificateNumber)
    SELECT @C1Id, Id, '2015-06-01', '2026-06-01', N'CERT-BM-001'
    FROM dbo.ServiceCategories WHERE Code = N'45200000-9';

    INSERT INTO dbo.ContractorServiceCategories (ContractorId, ServiceCategoryId, CertifiedAt, ExpiresAt, CertificateNumber)
    SELECT @C1Id, Id, '2018-01-20', '2027-01-20', N'CERT-BM-002'
    FROM dbo.ServiceCategories WHERE Code = N'45260000-7';

    -- Licenses (1:N)
    INSERT INTO dbo.ContractorLicenses (ContractorId, RegionId, LicenseNumber, IssuedAt, ValidUntil, IsActive)
    SELECT @C1Id, Id, N'LIC-KY-2015-001', '2015-06-01', '2027-06-01', 1
    FROM dbo.Regions WHERE Code = N'UA-32';

    INSERT INTO dbo.ContractorLicenses (ContractorId, RegionId, LicenseNumber, IssuedAt, ValidUntil, IsActive)
    SELECT @C1Id, Id, N'LIC-KH-2019-004', '2019-04-10', '2027-04-10', 1
    FROM dbo.Regions WHERE Code = N'UA-63';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Contractors WHERE EDRPOU = '27182818')
BEGIN
    INSERT INTO dbo.Contractors (Name, EDRPOU, LegalForm, RegistrationDate, AuthorizedCapital, IsActive, CreatedBy)
    VALUES (N'ПрАТ «ІнфраБуд Груп»', '27182818', N'ПрАТ', '2008-11-20', 2500000.00, 1, N'seed');

    DECLARE @C2Id BIGINT = SCOPE_IDENTITY();

    INSERT INTO dbo.ContractorProfiles (ContractorId, Website, ContactEmail, ContactPhone, HeadquartersAddress)
    VALUES (@C2Id, N'https://infrabud.com', N'tender@infrabud.com', N'+380572345678',
            N'м. Харків, пр. Науки, 55');

    INSERT INTO dbo.ContractorServiceCategories (ContractorId, ServiceCategoryId, CertifiedAt, ExpiresAt, CertificateNumber)
    SELECT @C2Id, Id, '2010-05-01', '2028-05-01', N'CERT-IB-001'
    FROM dbo.ServiceCategories WHERE Code = N'45220000-5';

    INSERT INTO dbo.ContractorLicenses (ContractorId, RegionId, LicenseNumber, IssuedAt, ValidUntil, IsActive)
    SELECT @C2Id, Id, N'LIC-KH-2010-002', '2010-05-01', '2028-05-01', 1
    FROM dbo.Regions WHERE Code = N'UA-63';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Contractors WHERE EDRPOU = '99887766')
BEGIN
    INSERT INTO dbo.Contractors (Name, EDRPOU, LegalForm, RegistrationDate, AuthorizedCapital, IsActive, CreatedBy)
    VALUES (N'ФОП Петренко В.О.', '99887766', N'ФОП', '2024-11-01', 1000.00, 1, N'seed');
    -- Note: registered 2024-11-01 with minimal capital — classic shell-company risk flag

    DECLARE @C3Id BIGINT = SCOPE_IDENTITY();

    INSERT INTO dbo.ContractorProfiles (ContractorId, ContactEmail, ContactPhone, HeadquartersAddress, Description)
    VALUES (@C3Id, N'petrenko.vo@gmail.com', N'+380501112233',
            N'м. Харків, вул. Нова, 1',
            N'Новозареєстрований підрядник. Звернути увагу: незначний статутний капітал.');

    INSERT INTO dbo.ContractorServiceCategories (ContractorId, ServiceCategoryId, CertifiedAt, ExpiresAt, CertificateNumber)
    SELECT @C3Id, Id, '2024-11-15', '2026-11-15', N'CERT-FOP-001'
    FROM dbo.ServiceCategories WHERE Code = N'45330000-9';

    INSERT INTO dbo.ContractorLicenses (ContractorId, RegionId, LicenseNumber, IssuedAt, ValidUntil, IsActive)
    SELECT @C3Id, Id, N'LIC-KH-2024-099', '2024-11-15', '2026-11-15', 1
    FROM dbo.Regions WHERE Code = N'UA-63';
END;
GO
