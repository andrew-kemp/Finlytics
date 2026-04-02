-- Bills / Accounts Payable tables
-- Run this against the financehub database

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Bills')
BEGIN
    CREATE TABLE Bills (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        BillNumber NVARCHAR(50) NOT NULL,
        SupplierId NVARCHAR(50) NULL,
        SupplierName NVARCHAR(255) NOT NULL,
        SupplierReference NVARCHAR(100) NULL,
        DateReceived DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        DateIssued DATETIME2 NOT NULL,
        DueDate DATETIME2 NULL,
        DatePaid DATETIME2 NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Draft',
        AmountNet DECIMAL(18,2) NOT NULL DEFAULT 0,
        VATAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        AmountGross DECIMAL(18,2) NOT NULL DEFAULT 0,
        VATApplicability NVARCHAR(50) NULL,
        Currency NVARCHAR(10) NULL DEFAULT 'GBP',
        Category NVARCHAR(100) NULL,
        CtTag NVARCHAR(20) NULL,
        PaymentMethod NVARCHAR(100) NULL,
        Notes NVARCHAR(MAX) NULL,
        TaxYear NVARCHAR(20) NULL,
        FinancialYear NVARCHAR(20) NULL,
        DocumentUrl NVARCHAR(2048) NULL,
        Attachments NVARCHAR(MAX) NULL,
        LineItems NVARCHAR(MAX) NULL,
        AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0,
        PaymentReference NVARCHAR(100) NULL,
        IsRecurring BIT NOT NULL DEFAULT 0,
        RecurringFrequency NVARCHAR(50) NULL,
        RecurringNextDate DATETIME2 NULL,
        ApprovedBy NVARCHAR(255) NULL,
        ApprovedAt DATETIME2 NULL,
        LinkedExpenseId INT NULL
    );

    CREATE INDEX IX_Bills_Status ON Bills([Status]);
    CREATE INDEX IX_Bills_SupplierId ON Bills(SupplierId);
    CREATE INDEX IX_Bills_DueDate ON Bills(DueDate);
    CREATE INDEX IX_Bills_BillNumber ON Bills(BillNumber);

    PRINT 'Created Bills table with indexes';
END
ELSE
BEGIN
    PRINT 'Bills table already exists';
END
