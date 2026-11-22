-- Lab 08: idempotency table and event-updated columns on the local contractor directory.
-- Applied at Tenders.Api startup (BiddingMessagingSchema). Safe to run more than once.
-- Fresh databases also get these objects from db/p1/schema.sql.

IF COL_LENGTH('dbo.Contractors', 'AuthorizedCapital') IS NULL
    ALTER TABLE dbo.Contractors ADD AuthorizedCapital DECIMAL(18,2) NULL;
GO

IF COL_LENGTH('dbo.Contractors', 'LastNameEventAt') IS NULL
    ALTER TABLE dbo.Contractors ADD LastNameEventAt DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.Contractors', 'LastCapitalEventAt') IS NULL
    ALTER TABLE dbo.Contractors ADD LastCapitalEventAt DATETIME2 NULL;
GO

IF OBJECT_ID(N'dbo.ProcessedEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessedEvents (
        EventId     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        EventType   NVARCHAR(200)    NOT NULL,
        ProcessedAt DATETIME2        NOT NULL CONSTRAINT DF_ProcessedEvents_ProcessedAt DEFAULT SYSUTCDATETIME()
    );
END
GO
