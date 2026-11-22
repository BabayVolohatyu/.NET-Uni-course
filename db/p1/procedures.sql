-- ============================================================
-- Project №1: Stored Procedures — Bidding / Procurement Context
-- SQL Server + ADO.NET & Dapper
-- Run after: schema.sql
-- ============================================================

USE ProcurementBidding;
GO

-- ============================================================
-- PROCEDURE 1: usp_CreateTender
-- Creates a Tender + its TenderDetails in one explicit transaction.
-- CRUD: INSERT
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateTender
    @ProcuringEntityId  BIGINT,
    @Title              NVARCHAR(500),
    @ExpectedBudget     DECIMAL(18,2),
    @Description        NVARCHAR(MAX),
    @CategoryCode       NVARCHAR(50)  = NULL,
    @DeliveryAddress    NVARCHAR(500),
    @TechnicalSpecUrl   NVARCHAR(1000) = NULL,
    @CreatedBy          NVARCHAR(100) = NULL,
    @NewTenderId        BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;   -- any error rolls back the transaction automatically

    BEGIN TRANSACTION;

    INSERT INTO dbo.Tenders
        (ProcuringEntityId, Title, ExpectedBudget, Status, CreatedBy)
    VALUES
        (@ProcuringEntityId, @Title, @ExpectedBudget, N'Draft', @CreatedBy);

    SET @NewTenderId = SCOPE_IDENTITY();

    INSERT INTO dbo.TenderDetails
        (TenderId, Description, CategoryCode, DeliveryAddress, TechnicalSpecUrl)
    VALUES
        (@NewTenderId, @Description, @CategoryCode, @DeliveryAddress, @TechnicalSpecUrl);

    COMMIT TRANSACTION;
END;
GO

-- ============================================================
-- PROCEDURE 2: usp_ChangeTenderStatus  ← TRANSACTIONAL STATUS CHANGE (key procedure)
-- Validates that the requested transition is legal; rejects it with a managed error if not.
-- Uses UPDLOCK to prevent concurrent status changes on the same tender.
--
-- Allowed transitions:
--   Draft              → Published
--   Published          → BiddingOpen
--   BiddingOpen        → EvaluationInProgress
--   EvaluationInProgress → Awarded
--   Draft / Published / BiddingOpen / EvaluationInProgress → Cancelled
--   Awarded / Cancelled  → (nothing — terminal states)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_ChangeTenderStatus
    @TenderId    BIGINT,
    @NewStatus   NVARCHAR(30),
    @ChangedBy   NVARCHAR(100) = NULL,
    @Reason      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Lock the row for update to prevent concurrent state changes
    DECLARE @CurrentStatus NVARCHAR(30) =
        (SELECT Status FROM dbo.Tenders WITH (UPDLOCK) WHERE Id = @TenderId AND IsDeleted = 0);

    IF @CurrentStatus IS NULL
        THROW 50001, 'Tender not found or has been deleted.', 1;

    -- Validate the transition
    IF NOT (
        (@CurrentStatus = N'Draft'               AND @NewStatus = N'Published')             OR
        (@CurrentStatus = N'Published'            AND @NewStatus = N'BiddingOpen')           OR
        (@CurrentStatus = N'BiddingOpen'          AND @NewStatus = N'EvaluationInProgress')  OR
        (@CurrentStatus = N'EvaluationInProgress' AND @NewStatus = N'Awarded')               OR
        (@CurrentStatus IN (N'Draft', N'Published', N'BiddingOpen', N'EvaluationInProgress')
                                                  AND @NewStatus = N'Cancelled')
    )
        THROW 50002, 'Invalid status transition. The requested transition is not permitted for the current tender status.', 1;

    -- Apply the transition
    UPDATE dbo.Tenders
    SET    Status    = @NewStatus,
           UpdatedAt = SYSUTCDATETIME(),
           UpdatedBy = @ChangedBy
    WHERE  Id = @TenderId;

    -- Record in audit history
    INSERT INTO dbo.TenderStatusHistory (TenderId, FromStatus, ToStatus, ChangedBy, Reason)
    VALUES (@TenderId, @CurrentStatus, @NewStatus, @ChangedBy, @Reason);

    COMMIT TRANSACTION;
END;
GO

-- ============================================================
-- PROCEDURE 3: usp_SubmitBid
-- Registers a contractor's bid for an open tender.
-- Business rule: bidding is only allowed when Status = 'BiddingOpen'.
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_SubmitBid
    @TenderId         BIGINT,
    @ContractorId     BIGINT,
    @ProposedPrice    DECIMAL(18,2),
    @PriceReductionPct DECIMAL(5,2) = 0,
    @CreatedBy        NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @TenderStatus NVARCHAR(30) =
        (SELECT Status FROM dbo.Tenders WITH (UPDLOCK) WHERE Id = @TenderId AND IsDeleted = 0);

    IF @TenderStatus IS NULL
        THROW 50003, 'Tender not found.', 1;

    IF @TenderStatus <> N'BiddingOpen'
        THROW 50004, 'Bids can only be submitted when the tender is in BiddingOpen status.', 1;

    -- Fetch contractor snapshot fields (name and EDRPOU at the moment of submission)
    DECLARE @ContractorName   NVARCHAR(300);
    DECLARE @ContractorEDRPOU CHAR(8);
    SELECT @ContractorName = Name, @ContractorEDRPOU = EDRPOU
    FROM dbo.Contractors WHERE Id = @ContractorId;

    IF @ContractorName IS NULL
        THROW 50005, 'Contractor not found in local registry snapshot.', 1;

    -- Upsert: if contractor already submitted a bid, update it; otherwise insert
    IF EXISTS (SELECT 1 FROM dbo.Bids WHERE TenderId = @TenderId AND ContractorId = @ContractorId)
    BEGIN
        UPDATE dbo.Bids
        SET  ProposedPrice     = @ProposedPrice,
             PriceReductionPct = @PriceReductionPct,
             ContractorName    = @ContractorName,
             ContractorEDRPOU  = @ContractorEDRPOU,
             UpdatedAt         = SYSUTCDATETIME(),
             UpdatedBy         = @CreatedBy,
             IsDeleted         = 0
        WHERE TenderId = @TenderId AND ContractorId = @ContractorId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.Bids
            (TenderId, ContractorId, ContractorName, ContractorEDRPOU,
             ProposedPrice, PriceReductionPct, CreatedBy)
        VALUES
            (@TenderId, @ContractorId, @ContractorName, @ContractorEDRPOU,
             @ProposedPrice, @PriceReductionPct, @CreatedBy);
    END;

    COMMIT TRANSACTION;
END;
GO

-- ============================================================
-- PROCEDURE 4: usp_AwardTender
-- Marks a specific bid as the winner and transitions the tender to Awarded.
-- Uses a transaction to keep bid + status change atomic.
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_AwardTender
    @TenderId        BIGINT,
    @WinnerContractorId BIGINT,
    @ChangedBy       NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @CurrentStatus NVARCHAR(30) =
        (SELECT Status FROM dbo.Tenders WITH (UPDLOCK) WHERE Id = @TenderId AND IsDeleted = 0);

    IF @CurrentStatus IS NULL
        THROW 50006, 'Tender not found.', 1;

    IF @CurrentStatus <> N'EvaluationInProgress'
        THROW 50007, 'A tender can only be awarded when it is in EvaluationInProgress status.', 1;

    -- Verify the winning bid exists and is not disqualified
    IF NOT EXISTS (
        SELECT 1 FROM dbo.Bids
        WHERE TenderId = @TenderId AND ContractorId = @WinnerContractorId
          AND IsDeleted = 0 AND IsDisqualified = 0)
        THROW 50008, 'The specified bid does not exist, was deleted, or has been disqualified.', 1;

    -- Mark winner
    UPDATE dbo.Bids
    SET  IsWinner  = 1,
         UpdatedAt = SYSUTCDATETIME(),
         UpdatedBy = @ChangedBy
    WHERE TenderId = @TenderId AND ContractorId = @WinnerContractorId;

    -- Transition status
    UPDATE dbo.Tenders
    SET  Status    = N'Awarded',
         UpdatedAt = SYSUTCDATETIME(),
         UpdatedBy = @ChangedBy
    WHERE Id = @TenderId;

    INSERT INTO dbo.TenderStatusHistory (TenderId, FromStatus, ToStatus, ChangedBy, Reason)
    VALUES (@TenderId, N'EvaluationInProgress', N'Awarded', @ChangedBy, N'Winning bid selected');

    COMMIT TRANSACTION;
END;
GO

-- ============================================================
-- PROCEDURE 5: usp_GetTendersForEntity
-- Returns active (non-deleted) tenders for a procuring entity, filtered by optional status.
-- CRUD: SELECT (read operation used by ADO.NET/Dapper DAL in Lab 02)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetTendersForEntity
    @ProcuringEntityId BIGINT,
    @Status            NVARCHAR(30) = NULL   -- NULL = all statuses
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.Id,
        t.Title,
        t.ExpectedBudget,
        t.Status,
        t.PublishedAt,
        t.BiddingDeadline,
        t.CreatedAt,
        pe.Name  AS ProcuringEntityName
    FROM dbo.Tenders      t
    JOIN dbo.ProcuringEntities pe ON pe.Id = t.ProcuringEntityId
    WHERE t.ProcuringEntityId = @ProcuringEntityId
      AND t.IsDeleted = 0
      AND (@Status IS NULL OR t.Status = @Status)
    ORDER BY t.CreatedAt DESC;
END;
GO

-- ============================================================
-- PROCEDURE 6: usp_GetBidsForTender
-- Returns all valid bids for a tender sorted by proposed price ascending (for evaluation).
-- CRUD: SELECT (read operation for evaluation committee view)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetBidsForTender
    @TenderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.ContractorId,
        b.ContractorName,
        b.ContractorEDRPOU,
        b.ProposedPrice,
        b.PriceReductionPct,
        b.SubmittedAt,
        b.IsWinner,
        b.IsDisqualified
    FROM dbo.Bids b
    WHERE b.TenderId    = @TenderId
      AND b.IsDeleted   = 0
    ORDER BY b.ProposedPrice ASC;
END;
GO
