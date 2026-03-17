-- Add Direction column to DlaEntries to track owed-to-company vs owed-to-director
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('DlaEntries') 
    AND name = 'Direction'
)
BEGIN
    ALTER TABLE [dbo].[DlaEntries]
    ADD [Direction] NVARCHAR(50) NULL;

    EXEC('UPDATE [dbo].[DlaEntries] SET [Direction] = ''OwedToDirector'' WHERE [Direction] IS NULL');

    PRINT 'Direction column added to DlaEntries';
END
ELSE
BEGIN
    PRINT 'Direction column already exists in DlaEntries';
END
