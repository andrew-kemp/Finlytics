-- Add Directors column to CompanySettings table
-- This column stores a comma-separated list of director names for DLA functionality

-- Check if the column already exists before adding it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'CompanySettings' AND COLUMN_NAME = 'Directors')
BEGIN
    ALTER TABLE CompanySettings 
    ADD Directors NVARCHAR(1000) NULL;
    
    PRINT 'Added Directors column to CompanySettings table';
END
ELSE
BEGIN
    PRINT 'Directors column already exists in CompanySettings table';
END

PRINT 'Migration completed successfully';
