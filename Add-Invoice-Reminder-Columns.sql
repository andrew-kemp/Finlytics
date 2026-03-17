-- Add invoice reminder tracking columns to Invoices table
-- Run once: adds ReminderSentAt and ReminderCount columns

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'ReminderSentAt'
)
BEGIN
    ALTER TABLE Invoices ADD ReminderSentAt DATETIME2 NULL;
    PRINT 'Added ReminderSentAt column to Invoices table.';
END
ELSE
BEGIN
    PRINT 'ReminderSentAt column already exists — skipping.';
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'ReminderCount'
)
BEGIN
    ALTER TABLE Invoices ADD ReminderCount INT NOT NULL DEFAULT 0;
    PRINT 'Added ReminderCount column to Invoices table.';
END
ELSE
BEGIN
    PRINT 'ReminderCount column already exists — skipping.';
END
