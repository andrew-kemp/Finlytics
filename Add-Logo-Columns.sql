-- Add DocumentLogoUrl and EmailLogoUrl columns to CompanySettings
-- DocumentLogoUrl: used for PDF documents (invoices, quotes, credit notes, payslips, etc.)
-- EmailLogoUrl: used in email template headers

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CompanySettings') AND name = 'DocumentLogoUrl')
BEGIN
    ALTER TABLE CompanySettings ADD DocumentLogoUrl NVARCHAR(500) NULL;
    PRINT 'Added DocumentLogoUrl column to CompanySettings';
END
ELSE
    PRINT 'DocumentLogoUrl column already exists';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CompanySettings') AND name = 'EmailLogoUrl')
BEGIN
    ALTER TABLE CompanySettings ADD EmailLogoUrl NVARCHAR(500) NULL;
    PRINT 'Added EmailLogoUrl column to CompanySettings';
END
ELSE
    PRINT 'EmailLogoUrl column already exists';
