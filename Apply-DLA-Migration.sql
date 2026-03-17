-- Add DLA Classification and Incorporation Date Fields
-- Migration: 20260226093506_AddDlaClassificationAndIncorporationDate

-- Show all tables so we can confirm names
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;

-- Show existing columns on DlaEntries
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DlaEntries' ORDER BY ORDINAL_POSITION;

BEGIN TRY
    -- Add ClassificationSource column to DlaEntries
    IF NOT EXISTS (
        SELECT * FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[DlaEntries]') 
        AND name = 'ClassificationSource'
    )
    BEGIN
        ALTER TABLE [dbo].[DlaEntries] ADD [ClassificationSource] nvarchar(max) NULL;
        PRINT 'Added ClassificationSource column to DlaEntries';
    END
    ELSE
    BEGIN
        PRINT 'ClassificationSource column already exists in DlaEntries';
    END;
END TRY
BEGIN CATCH
    PRINT 'Error adding ClassificationSource: ' + ERROR_MESSAGE();
END CATCH;

BEGIN TRY
    -- Add IncorporationDate column to CompanySettings
    IF NOT EXISTS (
        SELECT * FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[CompanySettings]') 
        AND name = 'IncorporationDate'
    )
    BEGIN
        ALTER TABLE [dbo].[CompanySettings] ADD [IncorporationDate] datetime2 NULL;
        PRINT 'Added IncorporationDate column to CompanySettings';
    END
    ELSE
    BEGIN
        PRINT 'IncorporationDate column already exists in CompanySettings';
    END;
END TRY
BEGIN CATCH
    PRINT 'Error adding IncorporationDate: ' + ERROR_MESSAGE();
END CATCH;

-- Backfill existing DLA entries using dynamic SQL to avoid compilation errors
BEGIN TRY
    DECLARE @sql NVARCHAR(MAX);
    SET @sql = N'UPDATE [dbo].[DlaEntries] SET [ClassificationSource] = ''manual'' WHERE [ClassificationSource] IS NULL OR [ClassificationSource] = ''''';
    EXEC sp_executesql @sql;
    PRINT 'Updated DLA entries with manual classification';
END TRY
BEGIN CATCH
    PRINT 'Error updating DlaEntries: ' + ERROR_MESSAGE();
END CATCH;

PRINT 'Migration completed';
