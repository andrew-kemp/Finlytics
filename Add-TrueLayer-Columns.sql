-- Add TrueLayer columns to BankAccounts and BankTransactions
-- Run this migration against FinanceHub database

-- BankAccounts: TrueLayer fields
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'TrueLayerAccountId')
    ALTER TABLE BankAccounts ADD TrueLayerAccountId NVARCHAR(255) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'TrueLayerConnected')
    ALTER TABLE BankAccounts ADD TrueLayerConnected BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'TrueLayerLastSyncedAt')
    ALTER TABLE BankAccounts ADD TrueLayerLastSyncedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'TrueLayerProvider')
    ALTER TABLE BankAccounts ADD TrueLayerProvider NVARCHAR(255) NULL;
GO

-- BankTransactions: TrueLayer fields
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankTransactions') AND name = 'TrueLayerTransactionId')
    ALTER TABLE BankTransactions ADD TrueLayerTransactionId NVARCHAR(255) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankTransactions') AND name = 'TrueLayerMerchantName')
    ALTER TABLE BankTransactions ADD TrueLayerMerchantName NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankTransactions') AND name = 'TrueLayerCategory')
    ALTER TABLE BankTransactions ADD TrueLayerCategory NVARCHAR(100) NULL;
GO

-- Optional index for TrueLayer account lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'IX_BankAccounts_TrueLayerAccountId')
    CREATE INDEX IX_BankAccounts_TrueLayerAccountId ON BankAccounts (TrueLayerAccountId) WHERE TrueLayerAccountId IS NOT NULL;
GO

-- Optional index for TrueLayer transaction lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('BankTransactions') AND name = 'IX_BankTransactions_TrueLayerTransactionId')
    CREATE INDEX IX_BankTransactions_TrueLayerTransactionId ON BankTransactions (TrueLayerTransactionId) WHERE TrueLayerTransactionId IS NOT NULL;
GO

PRINT 'TrueLayer migration completed successfully';
