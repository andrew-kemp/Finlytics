-- FinanceHub: Add Dividend Tables Migration
-- Run this script against the FinanceHub Azure SQL database
-- Creates DividendDeclarations and DividendAllocations tables

-- ─────────────────────────────────────────────────────────────────────────────
-- DividendDeclarations
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DividendDeclarations')
BEGIN
    CREATE TABLE DividendDeclarations (
        Id                INT             IDENTITY(1,1)   NOT NULL PRIMARY KEY,
        DividendRef       NVARCHAR(50)    NOT NULL,
        DividendType      NVARCHAR(50)    NOT NULL DEFAULT 'Interim',
        ShareClass        NVARCHAR(50)    NOT NULL DEFAULT '',
        MeetingDate       DATETIME2       NOT NULL,
        MeetingLocation   NVARCHAR(255)   NULL,
        RecordDate        DATETIME2       NOT NULL,
        PaymentDate       DATETIME2       NOT NULL,
        AmountPerShare    DECIMAL(18,4)   NOT NULL DEFAULT 0,
        TotalAmount       DECIMAL(18,2)   NOT NULL DEFAULT 0,
        [Status]          NVARCHAR(50)    NOT NULL DEFAULT 'Draft',
        DirectorName      NVARCHAR(255)   NULL,
        Notes             NVARCHAR(2000)  NULL,
        CreatedDate       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        FinalisedDate     DATETIME2       NULL
    );

    CREATE INDEX IX_DividendDeclarations_Ref    ON DividendDeclarations (DividendRef);
    CREATE INDEX IX_DividendDeclarations_Status ON DividendDeclarations ([Status]);

    PRINT 'Created table: DividendDeclarations';
END
ELSE
BEGIN
    PRINT 'Table DividendDeclarations already exists — skipped.';
END

-- ─────────────────────────────────────────────────────────────────────────────
-- DividendAllocations
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DividendAllocations')
BEGIN
    CREATE TABLE DividendAllocations (
        Id                      INT             IDENTITY(1,1)   NOT NULL PRIMARY KEY,
        DividendDeclarationId   INT             NOT NULL,
        ShareholderId           INT             NULL,
        ShareholderName         NVARCHAR(255)   NOT NULL,
        ShareClass              NVARCHAR(50)    NOT NULL DEFAULT '',
        NumberOfShares          INT             NOT NULL DEFAULT 0,
        AmountPerShare          DECIMAL(18,4)   NOT NULL DEFAULT 0,
        TotalAmount             DECIMAL(18,2)   NOT NULL DEFAULT 0,
        BankAccountName         NVARCHAR(255)   NULL,
        SortCode                NVARCHAR(20)    NULL,
        AccountNumber           NVARCHAR(50)    NULL,
        VoucherRef              NVARCHAR(50)    NULL,
        LedgerEntryId           INT             NULL,

        CONSTRAINT FK_DividendAllocations_Declaration
            FOREIGN KEY (DividendDeclarationId)
            REFERENCES DividendDeclarations (Id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_DividendAllocations_DeclarationId ON DividendAllocations (DividendDeclarationId);

    PRINT 'Created table: DividendAllocations';
END
ELSE
BEGIN
    PRINT 'Table DividendAllocations already exists — skipped.';
END

PRINT 'Dividend tables migration complete.';
