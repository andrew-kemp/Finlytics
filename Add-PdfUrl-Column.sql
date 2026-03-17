-- Add PdfUrl column to Invoices table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Invoices') AND name = 'PdfUrl')
BEGIN
    ALTER TABLE Invoices ADD PdfUrl nvarchar(500) NULL;
    PRINT 'PdfUrl column added successfully';
END
ELSE
BEGIN
    PRINT 'PdfUrl column already exists';
END
GO
