-- Add startup capture fields to DlaEntries
IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('DlaEntries')
    AND name = 'CtTag'
)
BEGIN
    ALTER TABLE [dbo].[DlaEntries]
    ADD [CtTag] NVARCHAR(20) NULL;
END

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('DlaEntries')
    AND name = 'IsStartupCost'
)
BEGIN
    ALTER TABLE [dbo].[DlaEntries]
    ADD [IsStartupCost] BIT NOT NULL DEFAULT(0);
END

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('DlaEntries')
    AND name = 'SourceBatchId'
)
BEGIN
    ALTER TABLE [dbo].[DlaEntries]
    ADD [SourceBatchId] NVARCHAR(60) NULL;
END
