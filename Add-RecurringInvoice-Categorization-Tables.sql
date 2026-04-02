-- ═══════════════════════════════════════════════════════════════════════════
-- Add Recurring Invoice Templates and Categorization Rules tables
-- Run against: financehub-sql-kemponline.database.windows.net / financehub
-- ═══════════════════════════════════════════════════════════════════════════

-- 1. RecurringInvoiceTemplates
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecurringInvoiceTemplates')
BEGIN
    CREATE TABLE [dbo].[RecurringInvoiceTemplates] (
        [Id]              INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerId]      NVARCHAR(50)   NULL,
        [CustomerName]    NVARCHAR(255)  NOT NULL,
        [BillingEmail]    NVARCHAR(255)  NULL,
        [POReference]     NVARCHAR(255)  NULL,
        [VatNumber]       NVARCHAR(50)   NULL,
        [Frequency]       NVARCHAR(20)   NOT NULL DEFAULT 'Monthly',
        [DayOfMonth]      INT            NOT NULL DEFAULT 1,
        [NextRunDate]     DATETIME2      NULL,
        [IsActive]        BIT            NOT NULL DEFAULT 1,
        [Notes]           NVARCHAR(2000) NULL,
        [DiscountPercent] DECIMAL(5,2)   NULL,
        [DiscountAmount]  DECIMAL(18,2)  NULL,
        [DiscountNote]    NVARCHAR(500)  NULL,
        [DefaultLineItems] NVARCHAR(MAX) NULL,
        [CreatedDate]     DATETIME2      NULL,
        [ModifiedDate]    DATETIME2      NULL
    );

    CREATE INDEX [IX_RecurringInvoiceTemplates_IsActive] ON [dbo].[RecurringInvoiceTemplates] ([IsActive]);
    CREATE INDEX [IX_RecurringInvoiceTemplates_NextRunDate] ON [dbo].[RecurringInvoiceTemplates] ([NextRunDate]);

    PRINT 'Created RecurringInvoiceTemplates table';
END
ELSE
    PRINT 'RecurringInvoiceTemplates table already exists';
GO

-- 2. CategorizationRules
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CategorizationRules')
BEGIN
    CREATE TABLE [dbo].[CategorizationRules] (
        [Id]             INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name]           NVARCHAR(255)  NOT NULL,
        [MatchPattern]   NVARCHAR(500)  NULL,
        [MatchField]     NVARCHAR(50)   NOT NULL DEFAULT 'Description',
        [Direction]      NVARCHAR(10)   NULL,
        [AmountMin]      DECIMAL(18,2)  NULL,
        [AmountMax]      DECIMAL(18,2)  NULL,
        [TargetCategory] NVARCHAR(100)  NOT NULL,
        [Priority]       INT            NOT NULL DEFAULT 100,
        [IsActive]       BIT            NOT NULL DEFAULT 1,
        [CreatedDate]    DATETIME2      NULL,
        [ModifiedDate]   DATETIME2      NULL
    );

    CREATE INDEX [IX_CategorizationRules_IsActive] ON [dbo].[CategorizationRules] ([IsActive]);
    CREATE INDEX [IX_CategorizationRules_Priority] ON [dbo].[CategorizationRules] ([Priority]);

    PRINT 'Created CategorizationRules table';
END
ELSE
    PRINT 'CategorizationRules table already exists';
GO
