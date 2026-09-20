-- =============================================================================
-- Context 1 — Procurement & Risk Engine
-- Seed Data — idempotent (IF NOT EXISTS guards on every block)
-- Cross-context anchors: ROAD-UA-001, BLDG-UA-002, BRDG-UA-003
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO

USE SchemaHunterProcurement;
GO

-- ─── risk_threshold_rules ─────────────────────────────────────────────────────
-- 5 rules covering the metrics evaluated by usp_ComputeRiskAssessment

IF NOT EXISTS (SELECT 1 FROM procurement.risk_threshold_rules WHERE rule_name = N'Single Bidder Alert')
    INSERT INTO procurement.risk_threshold_rules (rule_name, description, metric, threshold_value, severity_level)
    VALUES (N'Single Bidder Alert',
            N'Tender received only one bid — high manipulation risk.',
            N'SingleBidder', 1.0, N'High');

IF NOT EXISTS (SELECT 1 FROM procurement.risk_threshold_rules WHERE rule_name = N'Budget Ceiling Proximity')
    INSERT INTO procurement.risk_threshold_rules (rule_name, description, metric, threshold_value, severity_level)
    VALUES (N'Budget Ceiling Proximity',
            N'Winning bid is within 2 % of the expected budget — possible price leakage.',
            N'BudgetDeviation', 0.02, N'Critical');

IF NOT EXISTS (SELECT 1 FROM procurement.risk_threshold_rules WHERE rule_name = N'New Company Risk')
    INSERT INTO procurement.risk_threshold_rules (rule_name, description, metric, threshold_value, severity_level)
    VALUES (N'New Company Risk',
            N'Company registered less than 1 year before tender publication.',
            N'CompanyAge', 1.0, N'Medium');

IF NOT EXISTS (SELECT 1 FROM procurement.risk_threshold_rules WHERE rule_name = N'Same Legal Address')
    INSERT INTO procurement.risk_threshold_rules (rule_name, description, metric, threshold_value, severity_level)
    VALUES (N'Same Legal Address',
            N'Multiple bidders share the same registered address — cartel signal.',
            N'SameAddress', 2.0, N'High');

IF NOT EXISTS (SELECT 1 FROM procurement.risk_threshold_rules WHERE rule_name = N'Related Party Bid')
    INSERT INTO procurement.risk_threshold_rules (rule_name, description, metric, threshold_value, severity_level)
    VALUES (N'Related Party Bid',
            N'Beneficial owner overlap detected between supplier and procuring entity.',
            N'RelatedParty', 1.0, N'Critical');

GO

-- ─── suppliers ────────────────────────────────────────────────────────────────
-- 4 Ukrainian construction firms; EDRPOU codes follow the 8-digit format

IF NOT EXISTS (SELECT 1 FROM procurement.suppliers WHERE edrpou_code = '32855961')
    INSERT INTO procurement.suppliers (edrpou_code, legal_name, tax_status, registration_date, risk_score)
    VALUES ('32855961', N'ТОВ "Укрбуд Магістраль"', N'Active', '2005-03-14', 12.00);

IF NOT EXISTS (SELECT 1 FROM procurement.suppliers WHERE edrpou_code = '40112378')
    INSERT INTO procurement.suppliers (edrpou_code, legal_name, tax_status, registration_date, risk_score)
    VALUES ('40112378', N'ПрАТ "Дніпробудінвест"', N'Active', '2015-07-22', 25.00);

IF NOT EXISTS (SELECT 1 FROM procurement.suppliers WHERE edrpou_code = '44567890')
    INSERT INTO procurement.suppliers (edrpou_code, legal_name, tax_status, registration_date, risk_score)
    VALUES ('44567890', N'ТОВ "Харківдорбуд"', N'Active', '2023-11-01', 55.00);

IF NOT EXISTS (SELECT 1 FROM procurement.suppliers WHERE edrpou_code = '21034567')
    INSERT INTO procurement.suppliers (edrpou_code, legal_name, tax_status, registration_date, risk_score)
    VALUES ('21034567', N'ДП "Укравтодор-Захід"', N'Suspended', '1998-04-05', 70.00);

GO

-- ─── tenders ─────────────────────────────────────────────────────────────────
-- 4 tenders: 2 Awarded (linked to ROAD-UA-001 / BLDG-UA-002), 1 Active, 1 Cancelled
-- project_code values intentionally mirror Context 2 reconstruction_projects.asset_code

IF NOT EXISTS (SELECT 1 FROM procurement.tenders WHERE prozorro_id = 'UA-2024-01-15-000001-a')
    INSERT INTO procurement.tenders
        (prozorro_id, project_code, title, procuring_entity, expected_value, currency, status, published_at, deadline_at)
    VALUES (
        'UA-2024-01-15-000001-a',
        'ROAD-UA-001',
        N'Реконструкція автодороги М-07 Київ–Житомир (км 12–27)',
        N'Служба автомобільних доріг у Київській обл.',
        148000000.00, 'UAH', 'Awarded',
        '2024-01-15T10:00:00', '2024-02-15T17:00:00'
    );

IF NOT EXISTS (SELECT 1 FROM procurement.tenders WHERE prozorro_id = 'UA-2024-02-20-000002-b')
    INSERT INTO procurement.tenders
        (prozorro_id, project_code, title, procuring_entity, expected_value, currency, status, published_at, deadline_at)
    VALUES (
        'UA-2024-02-20-000002-b',
        'BLDG-UA-002',
        N'Відновлення школи №14 м. Харків (корпус А та Б)',
        N'Департамент освіти Харківської МР',
        37500000.00, 'UAH', 'Awarded',
        '2024-02-20T09:00:00', '2024-03-20T17:00:00'
    );

IF NOT EXISTS (SELECT 1 FROM procurement.tenders WHERE prozorro_id = 'UA-2024-04-10-000003-c')
    INSERT INTO procurement.tenders
        (prozorro_id, project_code, title, procuring_entity, expected_value, currency, status, published_at, deadline_at)
    VALUES (
        'UA-2024-04-10-000003-c',
        'BRDG-UA-003',
        N'Капітальний ремонт мосту через р. Інгулець на трасі Н-14',
        N'Служба автомобільних доріг у Херсонській обл.',
        92000000.00, 'UAH', 'Active',
        '2024-04-10T10:00:00', '2024-05-10T17:00:00'
    );

IF NOT EXISTS (SELECT 1 FROM procurement.tenders WHERE prozorro_id = 'UA-2024-03-05-000004-d')
    INSERT INTO procurement.tenders
        (prozorro_id, project_code, title, procuring_entity, expected_value, currency, status, published_at, deadline_at)
    VALUES (
        'UA-2024-03-05-000004-d',
        'ROAD-UA-001',
        N'Встановлення дорожніх огорож М-07 (км 12–20) — скасований лот',
        N'Служба автомобільних доріг у Київській обл.',
        8200000.00, 'UAH', 'Cancelled',
        '2024-03-05T08:00:00', NULL
    );

GO

-- ─── tender_bids ─────────────────────────────────────────────────────────────
-- 6 bids total.
-- Tender 1 (ROAD-UA-001): 3 bids — winner close to budget → BudgetDeviation flag
-- Tender 2 (BLDG-UA-002): 2 bids — normal competition
-- Tender 3 (bridge): 1 bid  — single-bidder anomaly
-- Tender 4 (cancelled): no bids

DECLARE @t1 BIGINT = (SELECT tender_id FROM procurement.tenders WHERE prozorro_id = 'UA-2024-01-15-000001-a');
DECLARE @t2 BIGINT = (SELECT tender_id FROM procurement.tenders WHERE prozorro_id = 'UA-2024-02-20-000002-b');
DECLARE @t3 BIGINT = (SELECT tender_id FROM procurement.tenders WHERE prozorro_id = 'UA-2024-04-10-000003-c');

DECLARE @s1 BIGINT = (SELECT supplier_id FROM procurement.suppliers WHERE edrpou_code = '32855961');
DECLARE @s2 BIGINT = (SELECT supplier_id FROM procurement.suppliers WHERE edrpou_code = '40112378');
DECLARE @s3 BIGINT = (SELECT supplier_id FROM procurement.suppliers WHERE edrpou_code = '44567890');
DECLARE @s4 BIGINT = (SELECT supplier_id FROM procurement.suppliers WHERE edrpou_code = '21034567');

-- Tender 1 bids
IF NOT EXISTS (SELECT 1 FROM procurement.tender_bids WHERE tender_id = @t1 AND supplier_id = @s1)
    INSERT INTO procurement.tender_bids (tender_id, supplier_id, offered_amount, submitted_at, is_winner)
    VALUES (@t1, @s1, 147500000.00, '2024-02-14T14:22:00', 1);   -- 99.7% of budget → BudgetDeviation Critical

IF NOT EXISTS (SELECT 1 FROM procurement.tender_bids WHERE tender_id = @t1 AND supplier_id = @s2)
    INSERT INTO procurement.tender_bids (tender_id, supplier_id, offered_amount, submitted_at, is_winner)
    VALUES (@t1, @s2, 132000000.00, '2024-02-14T11:05:00', 0);

IF NOT EXISTS (SELECT 1 FROM procurement.tender_bids WHERE tender_id = @t1 AND supplier_id = @s3)
    INSERT INTO procurement.tender_bids (tender_id, supplier_id, offered_amount, submitted_at, is_winner)
    VALUES (@t1, @s3, 80000000.00, '2024-02-13T16:40:00', 0);   -- well below avg → risk_score bump

-- Tender 2 bids
IF NOT EXISTS (SELECT 1 FROM procurement.tender_bids WHERE tender_id = @t2 AND supplier_id = @s1)
    INSERT INTO procurement.tender_bids (tender_id, supplier_id, offered_amount, submitted_at, is_winner)
    VALUES (@t2, @s1, 35200000.00, '2024-03-19T10:00:00', 0);

IF NOT EXISTS (SELECT 1 FROM procurement.tender_bids WHERE tender_id = @t2 AND supplier_id = @s2)
    INSERT INTO procurement.tender_bids (tender_id, supplier_id, offered_amount, submitted_at, is_winner)
    VALUES (@t2, @s2, 36800000.00, '2024-03-19T13:45:00', 1);

-- Tender 3 bid — single bidder
IF NOT EXISTS (SELECT 1 FROM procurement.tender_bids WHERE tender_id = @t3 AND supplier_id = @s4)
    INSERT INTO procurement.tender_bids (tender_id, supplier_id, offered_amount, submitted_at, is_winner)
    VALUES (@t3, @s4, 91000000.00, '2024-05-09T15:00:00', 0);   -- suspended supplier, single bid

GO

-- ─── risk_assessments ─────────────────────────────────────────────────────────
-- 3 assessments: Critical (Tender 1 — BudgetDeviation), High (Tender 2 — none yet computed,
-- pre-populated for demo), Low (Tender 3 — only SingleBidder score entered manually)

DECLARE @t1r BIGINT = (SELECT tender_id FROM procurement.tenders WHERE prozorro_id = 'UA-2024-01-15-000001-a');
DECLARE @t2r BIGINT = (SELECT tender_id FROM procurement.tenders WHERE prozorro_id = 'UA-2024-02-20-000002-b');
DECLARE @t3r BIGINT = (SELECT tender_id FROM procurement.tenders WHERE prozorro_id = 'UA-2024-04-10-000003-c');

DECLARE @rBudget BIGINT = (SELECT rule_id FROM procurement.risk_threshold_rules WHERE metric = 'BudgetDeviation');
DECLARE @rSingle BIGINT = (SELECT rule_id FROM procurement.risk_threshold_rules WHERE metric = 'SingleBidder');

IF NOT EXISTS (SELECT 1 FROM procurement.risk_assessments WHERE tender_id = @t1r)
    INSERT INTO procurement.risk_assessments
        (tender_id, severity_level, overall_score, anomaly_flags, assessed_at, reviewed_by_rule_id)
    VALUES (
        @t1r, 'Critical', 70.00,
        N'[{"metric":"BudgetDeviation","deviation_pct":0.34},{"metric":"SingleBidder","bid_count":3}]',
        '2024-02-16T08:00:00', @rBudget
    );

IF NOT EXISTS (SELECT 1 FROM procurement.risk_assessments WHERE tender_id = @t2r)
    INSERT INTO procurement.risk_assessments
        (tender_id, severity_level, overall_score, anomaly_flags, assessed_at, reviewed_by_rule_id)
    VALUES (
        @t2r, 'Low', 10.00,
        N'[]',
        '2024-03-22T09:30:00', NULL
    );

IF NOT EXISTS (SELECT 1 FROM procurement.risk_assessments WHERE tender_id = @t3r)
    INSERT INTO procurement.risk_assessments
        (tender_id, severity_level, overall_score, anomaly_flags, assessed_at, reviewed_by_rule_id)
    VALUES (
        @t3r, 'High', 45.00,
        N'[{"metric":"SingleBidder","bid_count":1}]',
        '2024-05-11T10:00:00', @rSingle
    );

GO

PRINT 'Context 1 seed data inserted successfully.';
GO
