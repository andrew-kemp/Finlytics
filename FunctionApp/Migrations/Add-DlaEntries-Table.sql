-- Migration: Add DlaEntries table for Directors Loan Account tracking
-- Date: 2026-01-17
-- Description: Creates DlaEntries table with unique DLA ID format (DLA-YYYY-NNNN) and indexes

-- Create DlaEntries table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DlaEntries' AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[DlaEntries] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [DlaId] NVARCHAR(50) NOT NULL,
        [Director] NVARCHAR(200) NULL,
        [Description] NVARCHAR(500) NOT NULL,
        [AmountNet] DECIMAL(18,2) NOT NULL,
        [VatAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [AmountGross] DECIMAL(18,2) NOT NULL,
        [Category] NVARCHAR(100) NULL,
        [EntryDate] DATETIME2(7) NOT NULL,
        [DatePaid] DATETIME2(7) NULL,
        [PaymentMethod] NVARCHAR(50) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [ReceiptUrl] NVARCHAR(500) NULL,
        [PdfUrl] NVARCHAR(500) NULL,
        [PeriodKey] NVARCHAR(20) NULL,
        [TaxYear] NVARCHAR(50) NULL,
        [FinancialYear] NVARCHAR(50) NULL,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_DlaEntries] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Create unique index on DlaId
    CREATE UNIQUE NONCLUSTERED INDEX [IX_DlaEntries_DlaId] 
    ON [dbo].[DlaEntries] ([DlaId] ASC);

    -- Create index on PeriodKey for period-based queries
    CREATE NONCLUSTERED INDEX [IX_DlaEntries_PeriodKey] 
    ON [dbo].[DlaEntries] ([PeriodKey] ASC);

    -- Create index on EntryDate for date-based queries
    CREATE NONCLUSTERED INDEX [IX_DlaEntries_EntryDate] 
    ON [dbo].[DlaEntries] ([EntryDate] DESC);

    PRINT 'DlaEntries table created successfully';
END
ELSE
BEGIN
    PRINT 'DlaEntries table already exists';
END
GO

-- Add Directors column to CompanySettings if it doesn't exist
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('CompanySettings') 
    AND name = 'Directors'
)
BEGIN
    ALTER TABLE [dbo].[CompanySettings]
    ADD [Directors] NVARCHAR(1000) NULL;
    
    PRINT 'Directors column added to CompanySettings';
END
ELSE
BEGIN
    PRINT 'Directors column already exists in CompanySettings';
END
GO

PRINT 'DLA Migration completed successfully';
