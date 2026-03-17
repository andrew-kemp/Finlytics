-- Add recurring expense columns to Expenses table
-- Run once: adds IsRecurring, RecurringFrequency, RecurringNextDate columns

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'IsRecurring'
)
BEGIN
    ALTER TABLE Expenses ADD IsRecurring BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsRecurring column to Expenses table.';
END
ELSE
BEGIN
    PRINT 'IsRecurring column already exists — skipping.';
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'RecurringFrequency'
)
BEGIN
    ALTER TABLE Expenses ADD RecurringFrequency NVARCHAR(20) NULL;
    PRINT 'Added RecurringFrequency column to Expenses table.';
END
ELSE
BEGIN
    PRINT 'RecurringFrequency column already exists — skipping.';
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'RecurringNextDate'
)
BEGIN
    ALTER TABLE Expenses ADD RecurringNextDate DATETIME2 NULL;
    PRINT 'Added RecurringNextDate column to Expenses table.';
END
ELSE
BEGIN
    PRINT 'RecurringNextDate column already exists — skipping.';
END
