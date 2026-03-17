-- ===================================================================
-- DLA Installment Payment System - Database Migration
-- Date: February 9, 2026
-- ===================================================================
-- This migration adds support for tracking installment payments on 
-- Director's Loan Account entries and linking them to Company Ledger
-- ===================================================================

-- Step 1: Add DLA Payment Tracking Fields to DlaEntries table
-- -------------------------------------------------------------------
PRINT 'Adding payment tracking fields to DlaEntries table...';

ALTER TABLE DlaEntries 
ADD Director NVARCHAR(255) NOT NULL DEFAULT '';

ALTER TABLE DlaEntries 
ADD PayInInstallments BIT NOT NULL DEFAULT 0;

ALTER TABLE DlaEntries 
ADD AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0;

ALTER TABLE DlaEntries 
ADD RemainingBalance DECIMAL(18,2) NOT NULL DEFAULT 0;

PRINT 'DlaEntries fields added successfully.';

-- Step 2: Add DLA Payment Linking Fields to CompanyLedgerEntries table
-- -------------------------------------------------------------------
PRINT 'Adding DLA payment linking fields to CompanyLedgerEntries table...';

ALTER TABLE CompanyLedgerEntries 
ADD DlaReference NVARCHAR(50) NULL;

ALTER TABLE CompanyLedgerEntries 
ADD IsFullPayment BIT NULL;

PRINT 'CompanyLedgerEntries fields added successfully.';

-- Step 3: Update Existing DLA Entries with Initial Values
-- -------------------------------------------------------------------
PRINT 'Updating existing DLA entries with remaining balance...';

UPDATE DlaEntries 
SET RemainingBalance = AmountGross 
WHERE RemainingBalance = 0;

PRINT 'Existing DLA entries updated successfully.';

-- Step 4: Verification Queries
-- -------------------------------------------------------------------
PRINT 'Migration completed. Running verification...';

-- Verify DlaEntries columns
SELECT 
    COUNT(*) AS TotalDlaEntries,
    SUM(CASE WHEN PayInInstallments = 1 THEN 1 ELSE 0 END) AS InstallmentPaymentCount,
    SUM(AmountPaid) AS TotalAmountPaid,
    SUM(RemainingBalance) AS TotalRemainingBalance
FROM DlaEntries;

-- Verify CompanyLedgerEntries columns
SELECT 
    COUNT(*) AS TotalLedgerEntries,
    SUM(CASE WHEN DlaReference IS NOT NULL THEN 1 ELSE 0 END) AS DlaPaymentCount
FROM CompanyLedgerEntries;

PRINT 'Migration verification completed.';
PRINT '===================================================================';
PRINT 'SUCCESS: All database migrations applied successfully!';
PRINT '===================================================================';
