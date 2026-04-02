-- =====================================================
-- GoCardless Integration Migration
-- Adds: GoCardless models tables + columns on existing tables
-- Date: 2026-03-21
-- =====================================================

-- 1. GoCardless Mandates table (tracks Direct Debit authorisations)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GoCardlessMandates')
BEGIN
    CREATE TABLE GoCardlessMandates (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CustomerId NVARCHAR(50) NULL,
        CustomerName NVARCHAR(255) NULL,
        GoCardlessMandateId NVARCHAR(100) NULL,
        GoCardlessCustomerId NVARCHAR(100) NULL,
        [Status] NVARCHAR(50) NULL,
        Scheme NVARCHAR(20) NULL,
        BankAccountHolder NVARCHAR(255) NULL,
        BankAccountEndDigits NVARCHAR(10) NULL,
        Reference NVARCHAR(100) NULL,
        CreatedDate DATETIME2 NULL,
        ActivatedDate DATETIME2 NULL,
        CancelledDate DATETIME2 NULL
    );
    CREATE INDEX IX_GoCardlessMandates_CustomerId ON GoCardlessMandates(CustomerId);
    CREATE INDEX IX_GoCardlessMandates_GoCardlessMandateId ON GoCardlessMandates(GoCardlessMandateId);
    CREATE INDEX IX_GoCardlessMandates_Status ON GoCardlessMandates([Status]);
    PRINT 'Created GoCardlessMandates table';
END
GO

-- 2. GoCardless Payments table (tracks individual payment collections)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GoCardlessPayments')
BEGIN
    CREATE TABLE GoCardlessPayments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceId INT NULL,
        InvoiceNumber NVARCHAR(50) NULL,
        GoCardlessPaymentId NVARCHAR(100) NULL,
        GoCardlessMandateId NVARCHAR(100) NULL,
        Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
        Currency NVARCHAR(10) NULL DEFAULT 'GBP',
        [Description] NVARCHAR(500) NULL,
        [Status] NVARCHAR(50) NULL,
        ChargeDate DATETIME2 NULL,
        PaidOutDate DATETIME2 NULL,
        FailureReason NVARCHAR(500) NULL,
        CreatedDate DATETIME2 NULL
    );
    CREATE INDEX IX_GoCardlessPayments_InvoiceId ON GoCardlessPayments(InvoiceId);
    CREATE INDEX IX_GoCardlessPayments_GoCardlessPaymentId ON GoCardlessPayments(GoCardlessPaymentId);
    CREATE INDEX IX_GoCardlessPayments_Status ON GoCardlessPayments([Status]);
    PRINT 'Created GoCardlessPayments table';
END
GO

-- 3. Add GoCardless columns to BankAccounts
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankAccounts' AND COLUMN_NAME = 'GoCardlessRequisitionId')
BEGIN
    ALTER TABLE BankAccounts ADD GoCardlessRequisitionId NVARCHAR(255) NULL;
    ALTER TABLE BankAccounts ADD GoCardlessAccountId NVARCHAR(255) NULL;
    ALTER TABLE BankAccounts ADD GoCardlessConnected BIT NOT NULL DEFAULT 0;
    ALTER TABLE BankAccounts ADD GoCardlessLastSyncedAt DATETIME2 NULL;
    ALTER TABLE BankAccounts ADD GoCardlessInstitutionId NVARCHAR(100) NULL;
    PRINT 'Added GoCardless columns to BankAccounts';
END
GO

-- 4. Add GoCardless columns to Invoices
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'GoCardlessPaymentId')
BEGIN
    ALTER TABLE Invoices ADD GoCardlessPaymentId NVARCHAR(100) NULL;
    ALTER TABLE Invoices ADD GoCardlessMandateId NVARCHAR(100) NULL;
    ALTER TABLE Invoices ADD PaymentLink NVARCHAR(2000) NULL;
    PRINT 'Added GoCardless columns to Invoices';
END
GO

-- 5. Add GoCardless columns to Customers
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Customers' AND COLUMN_NAME = 'GoCardlessMandateId')
BEGIN
    ALTER TABLE Customers ADD GoCardlessMandateId NVARCHAR(100) NULL;
    ALTER TABLE Customers ADD GoCardlessCustomerId NVARCHAR(100) NULL;
    ALTER TABLE Customers ADD GoCardlessMandateStatus NVARCHAR(50) NULL;
    PRINT 'Added GoCardless columns to Customers';
END
GO

-- 6. Add GoCardless columns to FinanceHubSettings
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'FinanceHubSettings' AND COLUMN_NAME = 'GoCardlessAccessToken')
BEGIN
    ALTER TABLE FinanceHubSettings ADD GoCardlessAccessToken NVARCHAR(500) NULL;
    ALTER TABLE FinanceHubSettings ADD GoCardlessSecretId NVARCHAR(255) NULL;
    ALTER TABLE FinanceHubSettings ADD GoCardlessSecretKey NVARCHAR(255) NULL;
    ALTER TABLE FinanceHubSettings ADD GoCardlessWebhookSecret NVARCHAR(255) NULL;
    ALTER TABLE FinanceHubSettings ADD GoCardlessSandbox BIT NOT NULL DEFAULT 1;
    ALTER TABLE FinanceHubSettings ADD GoCardlessBankDataEnabled BIT NOT NULL DEFAULT 0;
    ALTER TABLE FinanceHubSettings ADD GoCardlessPaymentsEnabled BIT NOT NULL DEFAULT 0;
    PRINT 'Added GoCardless columns to FinanceHubSettings';
END
GO

PRINT 'GoCardless migration complete';
