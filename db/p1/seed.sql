-- ============================================================
-- Project №1: Seed Data — Bidding / Procurement Context
-- SQL Server + ADO.NET & Dapper
-- Run order: schema.sql → procedures.sql → seed.sql
-- IDEMPOTENT: safe to run multiple times — uses IF NOT EXISTS checks before every insert.
-- ============================================================

USE ProcurementBidding;
GO

-- ============================================================
-- Seed: ProcuringEntities
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProcuringEntities WHERE EDRPOU = '12345678')
INSERT INTO dbo.ProcuringEntities (Name, EDRPOU, Region, ContactEmail, CreatedBy)
VALUES (N'Київська міська рада (Відділ відновлення)', '12345678', N'Київська', N'tender@kyiv.gov.ua', N'seed');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ProcuringEntities WHERE EDRPOU = '87654321')
INSERT INTO dbo.ProcuringEntities (Name, EDRPOU, Region, ContactEmail, CreatedBy)
VALUES (N'Харківська обласна адміністрація', '87654321', N'Харківська', N'zakupivli@kharkiv.gov.ua', N'seed');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ProcuringEntities WHERE EDRPOU = '11223344')
INSERT INTO dbo.ProcuringEntities (Name, EDRPOU, Region, ContactEmail, CreatedBy)
VALUES (N'Миколаївська міська рада', '11223344', N'Миколаївська', N'tender@mykolaiv.gov.ua', N'seed');
GO

-- ============================================================
-- Seed: Contractors (local snapshots — updated via Registry events)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Contractors WHERE Id = 101)
INSERT INTO dbo.Contractors (Id, Name, EDRPOU)
VALUES (101, N'ТОВ «БудМайстер»', '31415926');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Contractors WHERE Id = 102)
INSERT INTO dbo.Contractors (Id, Name, EDRPOU)
VALUES (102, N'ПрАТ «ІнфраБуд Груп»', '27182818');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Contractors WHERE Id = 103)
INSERT INTO dbo.Contractors (Id, Name, EDRPOU)
VALUES (103, N'ФОП Петренко В.О.', '99887766');
GO

-- ============================================================
-- Seed: Tenders (use procedure for consistent creation)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Tenders WHERE Title = N'Відновлення школи №15 — покрівля та фасад')
BEGIN
    DECLARE @PEId1 BIGINT = (SELECT Id FROM dbo.ProcuringEntities WHERE EDRPOU = '12345678');
    DECLARE @T1Id  BIGINT;
    EXEC dbo.usp_CreateTender
        @ProcuringEntityId = @PEId1,
        @Title             = N'Відновлення школи №15 — покрівля та фасад',
        @ExpectedBudget    = 4500000.00,
        @Description       = N'Капітальний ремонт покрівлі та фасаду школи №15 у Дарницькому районі. Площа покрівлі — 1200 м².',
        @CategoryCode      = N'45261000-4',
        @DeliveryAddress   = N'м. Київ, вул. Дарницька, 15',
        @TechnicalSpecUrl  = N'https://prozorro.gov.ua/tender/UA-2026-01-001/spec',
        @CreatedBy         = N'seed',
        @NewTenderId       = @T1Id OUTPUT;

    -- Transition to Published for demonstration
    EXEC dbo.usp_ChangeTenderStatus @TenderId = @T1Id, @NewStatus = N'Published',  @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeTenderStatus @TenderId = @T1Id, @NewStatus = N'BiddingOpen', @ChangedBy = N'seed',
         @Reason = N'Bidding period opened per procurement schedule';

    -- Submit bids from two contractors
    EXEC dbo.usp_SubmitBid @TenderId = @T1Id, @ContractorId = 101, @ProposedPrice = 4200000.00, @CreatedBy = N'seed';
    EXEC dbo.usp_SubmitBid @TenderId = @T1Id, @ContractorId = 102, @ProposedPrice = 3980000.00, @CreatedBy = N'seed';

    -- Move to evaluation
    EXEC dbo.usp_ChangeTenderStatus @TenderId = @T1Id, @NewStatus = N'EvaluationInProgress', @ChangedBy = N'seed';

    -- Award to the lower bid (contractor 102)
    EXEC dbo.usp_AwardTender @TenderId = @T1Id, @WinnerContractorId = 102, @ChangedBy = N'seed';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Tenders WHERE Title = N'Ремонт теплотраси — мікрорайон «Залютине»')
BEGIN
    DECLARE @PEId2 BIGINT = (SELECT Id FROM dbo.ProcuringEntities WHERE EDRPOU = '87654321');
    DECLARE @T2Id  BIGINT;
    EXEC dbo.usp_CreateTender
        @ProcuringEntityId = @PEId2,
        @Title             = N'Ремонт теплотраси — мікрорайон «Залютине»',
        @ExpectedBudget    = 12000000.00,
        @Description       = N'Заміна застарілих труб теплопостачання діаметром DN200 на ділянці 1,8 км.',
        @CategoryCode      = N'44163000-0',
        @DeliveryAddress   = N'м. Харків, мікрорайон «Залютине»',
        @CreatedBy         = N'seed',
        @NewTenderId       = @T2Id OUTPUT;

    EXEC dbo.usp_ChangeTenderStatus @TenderId = @T2Id, @NewStatus = N'Published',  @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeTenderStatus @TenderId = @T2Id, @NewStatus = N'BiddingOpen', @ChangedBy = N'seed';

    -- Only one bidder — anomaly pattern: single-bidder tender
    EXEC dbo.usp_SubmitBid @TenderId = @T2Id, @ContractorId = 101, @ProposedPrice = 11950000.00, @CreatedBy = N'seed';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Tenders WHERE Title = N'Відбудова пішохідного мосту через р. Інгул')
BEGIN
    DECLARE @PEId3 BIGINT = (SELECT Id FROM dbo.ProcuringEntities WHERE EDRPOU = '11223344');
    DECLARE @T3Id  BIGINT;
    EXEC dbo.usp_CreateTender
        @ProcuringEntityId = @PEId3,
        @Title             = N'Відбудова пішохідного мосту через р. Інгул',
        @ExpectedBudget    = 28000000.00,
        @Description       = N'Демонтаж зруйнованого мосту та будівництво нового пішохідного мосту прогоном 60 м.',
        @CategoryCode      = N'45221100-3',
        @DeliveryAddress   = N'м. Миколаїв, вул. Набережна, 1',
        @CreatedBy         = N'seed',
        @NewTenderId       = @T3Id OUTPUT;
    -- Tender left in Draft — not yet published
END;
GO
