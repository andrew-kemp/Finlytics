-- ============================================================
-- Add HMRC RTI / FPS columns to payroll tables
-- Run this script against your Azure SQL database BEFORE
-- deploying the updated FunctionApp.
-- Safe to re-run (all statements are IF NOT EXISTS guarded).
-- ============================================================

-- ── PayrollRuns ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TaxYear')
    ALTER TABLE PayrollRuns ADD TaxYear NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TaxMonth')
    ALTER TABLE PayrollRuns ADD TaxMonth INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'FpsStatus')
    ALTER TABLE PayrollRuns ADD FpsStatus NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'FpsSubmittedAt')
    ALTER TABLE PayrollRuns ADD FpsSubmittedAt DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'FpsCorrelationId')
    ALTER TABLE PayrollRuns ADD FpsCorrelationId NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TotalGross')
    ALTER TABLE PayrollRuns ADD TotalGross DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TotalTax')
    ALTER TABLE PayrollRuns ADD TotalTax DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TotalEmployeeNi')
    ALTER TABLE PayrollRuns ADD TotalEmployeeNi DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TotalEmployerNi')
    ALTER TABLE PayrollRuns ADD TotalEmployerNi DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'TotalNetPay')
    ALTER TABLE PayrollRuns ADD TotalNetPay DECIMAL(18,2) NULL;

-- ── Payslips ──────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'EmployeeName')
    ALTER TABLE Payslips ADD EmployeeName NVARCHAR(255) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'TaxCode')
    ALTER TABLE Payslips ADD TaxCode NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'NiCategory')
    ALTER TABLE Payslips ADD NiCategory NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'NiNumber')
    ALTER TABLE Payslips ADD NiNumber NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'EmployerNi')
    ALTER TABLE Payslips ADD EmployerNi DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'TaxYear')
    ALTER TABLE Payslips ADD TaxYear NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'TaxMonth')
    ALTER TABLE Payslips ADD TaxMonth INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'YtdGross')
    ALTER TABLE Payslips ADD YtdGross DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'YtdTax')
    ALTER TABLE Payslips ADD YtdTax DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'YtdEmployeeNi')
    ALTER TABLE Payslips ADD YtdEmployeeNi DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'YtdEmployerNi')
    ALTER TABLE Payslips ADD YtdEmployerNi DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'StarterDeclaration')
    ALTER TABLE Payslips ADD StarterDeclaration NVARCHAR(5) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payslips') AND name = 'DirectorsNiMethod')
    ALTER TABLE Payslips ADD DirectorsNiMethod NVARCHAR(10) NULL;

-- ── PayrollSettings ───────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollSettings') AND name = 'PayDayOfMonth')
    ALTER TABLE PayrollSettings ADD PayDayOfMonth INT NULL DEFAULT 25;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollSettings') AND name = 'EmploymentAllowanceEligible')
    ALTER TABLE PayrollSettings ADD EmploymentAllowanceEligible BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PayrollSettings') AND name = 'SmallEmployerRelief')
    ALTER TABLE PayrollSettings ADD SmallEmployerRelief BIT NOT NULL DEFAULT 0;

-- ── Helpful index for YTD queries ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('PayrollRuns') AND name = 'IX_PayrollRuns_TaxYear_TaxMonth')
    CREATE INDEX IX_PayrollRuns_TaxYear_TaxMonth ON PayrollRuns (TaxYear, TaxMonth);

PRINT 'Payroll RTI column migration complete.';
