-- Add PdfUrl and LineItems columns to Quotes table
-- Run this script in Azure Portal Query Editor for the FinanceHub database

-- Check if PdfUrl column exists before adding it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Quotes]') AND name = 'PdfUrl')
BEGIN
    ALTER TABLE Quotes ADD PdfUrl nvarchar(500) NULL;
    PRINT 'PdfUrl column added successfully to Quotes';
END
ELSE
BEGIN
    PRINT 'PdfUrl column already exists in Quotes';
END
GO

-- Check if LineItems column exists before adding it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Quotes]') AND name = 'LineItems')
BEGIN
    ALTER TABLE Quotes ADD LineItems nvarchar(max) NULL;
    PRINT 'LineItems column added successfully to Quotes';
END
ELSE
BEGIN
    PRINT 'LineItems column already exists in Quotes';
END
GO
