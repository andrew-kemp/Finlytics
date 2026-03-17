-- Apply-Expense-CtTag-Migration.sql
-- Adds CtTag column to Expenses table and backfills based on Category
-- CtTag values: Revenue | Capital | NonCT
-- Run against Azure SQL Database

-- Add CtTag column if it doesn't already exist
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') 
    AND name = 'CtTag'
)
BEGIN
    ALTER TABLE [dbo].[Expenses] ADD [CtTag] nvarchar(20) NULL;
    PRINT 'Added CtTag column to Expenses table';
END
ELSE
BEGIN
    PRINT 'CtTag column already exists on Expenses table';
END

-- Backfill: Set NonCT for known disallowable categories (UK CT rules)
UPDATE [dbo].[Expenses]
SET [CtTag] = 'NonCT'
WHERE [Category] IN ('Client Entertainment', 'Client Gifts')
  AND ([CtTag] IS NULL OR [CtTag] = '');

-- Backfill: Default all remaining NULLs to Revenue
UPDATE [dbo].[Expenses]
SET [CtTag] = 'Revenue'
WHERE [CtTag] IS NULL OR [CtTag] = '';

PRINT 'Backfill complete. Row counts:';
SELECT [CtTag], COUNT(*) AS [Count] FROM [dbo].[Expenses] GROUP BY [CtTag];
