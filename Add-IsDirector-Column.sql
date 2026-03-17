-- Add IsDirector column to Employees table
-- This migration adds the Employee.IsDirector field needed for the new director management functionality

-- Check if the column already exists before adding it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Employees' AND COLUMN_NAME = 'IsDirector')
BEGIN
    ALTER TABLE Employees 
    ADD IsDirector BIT NOT NULL DEFAULT 0;
    
    PRINT 'Added IsDirector column to Employees table with default value False';
END
ELSE
BEGIN
    PRINT 'IsDirector column already exists in Employees table';
END

-- Optional: Update existing employees to mark them as directors if needed
-- Uncomment and modify the following lines to mark specific employees as directors:

-- UPDATE Employees SET IsDirector = 1 WHERE Name = 'Andy Kemp';
-- UPDATE Employees SET IsDirector = 1 WHERE Name = 'Director Name 2';

PRINT 'Migration completed successfully';