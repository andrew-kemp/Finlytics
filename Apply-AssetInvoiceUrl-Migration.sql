-- Manual application of EF migration: 20260301112032_AddAssetInvoiceUrl
-- Run this against the Azure SQL database (financehub) when the EF auto-migration has failed
-- because some columns already existed from a previous manual deployment.
-- This script is idempotent (safe to run multiple times).

PRINT 'Applying migration: 20260301112032_AddAssetInvoiceUrl';

-- ── 1. Expenses.CtTag ────────────────────────────────────────────────────────
BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'CtTag'
    )
    BEGIN
        ALTER TABLE [dbo].[Expenses] ADD [CtTag] nvarchar(20) NULL;
        PRINT 'Added CtTag to Expenses';
    END
    ELSE
        PRINT 'CtTag already exists in Expenses - skipped';
END TRY
BEGIN CATCH
    PRINT 'Error adding CtTag to Expenses: ' + ERROR_MESSAGE();
END CATCH;

-- ── 2. CompanySettings.PsaApproved ──────────────────────────────────────────
BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[CompanySettings]') AND name = 'PsaApproved'
    )
    BEGIN
        ALTER TABLE [dbo].[CompanySettings] ADD [PsaApproved] bit NULL;
        PRINT 'Added PsaApproved to CompanySettings';
    END
    ELSE
        PRINT 'PsaApproved already exists in CompanySettings - skipped';
END TRY
BEGIN CATCH
    PRINT 'Error adding PsaApproved to CompanySettings: ' + ERROR_MESSAGE();
END CATCH;

-- ── 3. CompanySettings.PsaContactName ───────────────────────────────────────
BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[CompanySettings]') AND name = 'PsaContactName'
    )
    BEGIN
        ALTER TABLE [dbo].[CompanySettings] ADD [PsaContactName] nvarchar(255) NULL;
        PRINT 'Added PsaContactName to CompanySettings';
    END
    ELSE
        PRINT 'PsaContactName already exists in CompanySettings - skipped';
END TRY
BEGIN CATCH
    PRINT 'Error adding PsaContactName to CompanySettings: ' + ERROR_MESSAGE();
END CATCH;

-- ── 4. Assets.InvoiceUrl ─────────────────────────────────────────────────────
BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Assets]') AND name = 'InvoiceUrl'
    )
    BEGIN
        ALTER TABLE [dbo].[Assets] ADD [InvoiceUrl] nvarchar(2048) NULL;
        PRINT 'Added InvoiceUrl to Assets';
    END
    ELSE
        PRINT 'InvoiceUrl already exists in Assets - skipped';
END TRY
BEGIN CATCH
    PRINT 'Error adding InvoiceUrl to Assets: ' + ERROR_MESSAGE();
END CATCH;

-- ── 5. Mark migration as applied in EF history ──────────────────────────────
BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[__EFMigrationsHistory]
        WHERE [MigrationId] = '20260301112032_AddAssetInvoiceUrl'
    )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES ('20260301112032_AddAssetInvoiceUrl', '8.0.0');
        PRINT 'Migration recorded in __EFMigrationsHistory';
    END
    ELSE
        PRINT 'Migration already in __EFMigrationsHistory - skipped';
END TRY
BEGIN CATCH
    PRINT 'Error updating __EFMigrationsHistory: ' + ERROR_MESSAGE();
END CATCH;

PRINT 'Done.';

-- Verify
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Assets', 'Expenses', 'CompanySettings')
  AND COLUMN_NAME IN ('InvoiceUrl', 'CtTag', 'PsaApproved', 'PsaContactName')
ORDER BY TABLE_NAME, COLUMN_NAME;

SELECT MigrationId FROM [dbo].[__EFMigrationsHistory] ORDER BY MigrationId;
