-- Add VatNumber column to Invoices table
-- Run this script in Azure Portal Query Editor for the FinanceHub database

-- Check if VatNumber column exists before adding it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Invoices]') AND name = 'VatNumber')
BEGIN
    ALTER TABLE Invoices ADD VatNumber nvarchar(50) NULL;
    PRINT 'VatNumber column added successfully to Invoices';
END
ELSE
BEGIN
    PRINT 'VatNumber column already exists in Invoices';
END
GO
