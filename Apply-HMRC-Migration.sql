-- ============================================================
-- Apply-HMRC-Migration.sql
-- Creates the HmrcTokens table for HMRC MTD OAuth token storage.
-- Run this once against your Azure SQL database.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'HmrcTokens'
)
BEGIN
    CREATE TABLE HmrcTokens (
        Id            INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
        AccessToken   NVARCHAR(MAX)      NOT NULL,
        RefreshToken  NVARCHAR(MAX)      NULL,
        ExpiresAt     DATETIME2          NOT NULL,
        Scope         NVARCHAR(500)      NULL,
        CreatedAt     DATETIME2          NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt     DATETIME2          NULL
    );
    PRINT 'HmrcTokens table created.';
END
ELSE
BEGIN
    PRINT 'HmrcTokens table already exists — skipped.';
END
