-- Add DLA tracking columns to CompanyLedger table

-- Check if DlaReference column exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CompanyLedger]') AND name = 'DlaReference')
BEGIN
    ALTER TABLE [dbo].[CompanyLedger]
    ADD [DlaReference] NVARCHAR(50) NULL;
    PRINT 'Added DlaReference column';
END
ELSE
BEGIN
    PRINT 'DlaReference column already exists';
END

-- Check if IsFullPayment column exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CompanyLedger]') AND name = 'IsFullPayment')
BEGIN
    ALTER TABLE [dbo].[CompanyLedger]
    ADD [IsFullPayment] BIT NULL;
    PRINT 'Added IsFullPayment column';
END
ELSE
BEGIN
    PRINT 'IsFullPayment column already exists';
END

PRINT 'CompanyLedger DLA columns migration complete';
