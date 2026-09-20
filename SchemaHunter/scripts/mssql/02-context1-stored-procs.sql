-- =============================================================================
-- Context 1 — Procurement & Risk Engine
-- Stored Procedures
-- Source: context1-procurement-dapper-guide.md  §4
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
GO

SET ANSI_NULLS ON;
GO

USE SchemaHunterProcurement;
GO

-- ─── §4.1  usp_UpsertTender ──────────────────────────────────────────────────
-- Insert or update tender by Prozorro ID.
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

-- ─── §4.2  usp_SubmitBid ─────────────────────────────────────────────────────
-- Register a bid and recalculate supplier risk score.
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

-- ─── §4.3  usp_GetTendersPaged ───────────────────────────────────────────────
-- Keyset-paginated tender list with optional status / project filters.
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

-- ─── §4.4  usp_SoftDeleteSupplier ────────────────────────────────────────────
-- Soft delete supplier and withdraw all open bids.
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

-- ─── §4.5  usp_ComputeRiskAssessment ─────────────────────────────────────────
-- Evaluate rules and write risk assessment for a tender.
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

-- ─── §4.6  usp_GetRiskReportByProject ────────────────────────────────────────
-- Aggregated risk summary per project code.
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

PRINT 'Context 1 stored procedures created successfully.';
GO
