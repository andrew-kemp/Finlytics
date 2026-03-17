-- Add LineItems column to Invoices table to store line items as JSON
-- This will allow invoice line items to be persisted in the database

-- Add the column if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'LineItems')
BEGIN
    ALTER TABLE Invoices 
    ADD LineItems nvarchar(max) NULL;
    
    PRINT 'LineItems column added successfully';
END
ELSE
BEGIN
    PRINT 'LineItems column already exists';
END
GO
