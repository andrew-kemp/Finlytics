-- Add VatEffectiveDate column to CompanySettings
-- This records when VAT registration became effective so we don't flag
-- quarters before this date as unfiled.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CompanySettings' AND COLUMN_NAME = 'VatEffectiveDate'
)
BEGIN
    ALTER TABLE CompanySettings ADD VatEffectiveDate DATETIME2 NULL;
    PRINT 'Added VatEffectiveDate column to CompanySettings';
END
ELSE
BEGIN
    PRINT 'VatEffectiveDate column already exists';
END
