-- Add UTR (Unique Taxpayer Reference) column to CompanySettings
-- Run this against: financehub-sql-kemponline.database.windows.net / financehub

BEGIN TRY
    IF NOT EXISTS (
        SELECT * FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[CompanySettings]') 
        AND name = 'Utr'
    )
    BEGIN
        ALTER TABLE [dbo].[CompanySettings] ADD [Utr] nvarchar(20) NULL;
        PRINT 'Added Utr column to CompanySettings';
    END
    ELSE
    BEGIN
        PRINT 'Utr column already exists in CompanySettings';
    END;
END TRY
BEGIN CATCH
    PRINT 'Error adding Utr: ' + ERROR_MESSAGE();
END CATCH;

PRINT 'Migration completed';
