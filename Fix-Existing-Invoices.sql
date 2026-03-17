-- =====================================================
-- Fix Existing Invoices with Null CustomerId
-- =====================================================
-- This script updates invoices that have CustomerName but null CustomerId
-- It resolves the customer code from the CustomerName field and sets the CustomerId

-- Step 1: View all broken invoices (invoices with null CustomerId)
SELECT 
    Id,
    InvoiceNumber,
    CustomerName,
    CustomerId,
    DateIssued,
    AmountGross
FROM Invoices
WHERE CustomerId IS NULL
ORDER BY DateIssued DESC;

-- Step 2: View customer codes to understand the mapping
SELECT 
    Id,
    Code,
    CustomerCode,
    Name,
    CustomerName
FROM Customers
ORDER BY Code;

-- Step 3: Update invoices with null CustomerId by matching CustomerName
-- This handles cases where CustomerName contains customer code like "ESK001 - Esken1"
UPDATE i
SET 
    i.CustomerId = CAST(c.Id AS INT),
    i.CustomerName = COALESCE(c.CustomerName, c.Name)
FROM Invoices i
INNER JOIN Customers c ON (
    -- Match by extracting code from "CODE - Name" format
    CASE 
        WHEN CHARINDEX(' - ', i.CustomerName) > 0 
        THEN LEFT(i.CustomerName, CHARINDEX(' - ', i.CustomerName) - 1)
        ELSE i.CustomerName
    END = c.Code
    OR 
    CASE 
        WHEN CHARINDEX(' - ', i.CustomerName) > 0 
        THEN LEFT(i.CustomerName, CHARINDEX(' - ', i.CustomerName) - 1)
        ELSE i.CustomerName
    END = c.CustomerCode
)
WHERE i.CustomerId IS NULL 
  AND i.CustomerName IS NOT NULL;

-- Step 4: Verify the fix
SELECT 
    i.Id,
    i.InvoiceNumber,
    i.CustomerName,
    i.CustomerId,
    c.Code as CustomerCode,
    c.Name as ActualCustomerName,
    i.DateIssued,
    i.AmountGross
FROM Invoices i
LEFT JOIN Customers c ON c.Id = CAST(i.CustomerId AS NVARCHAR(50))
WHERE i.DateIssued >= '2026-02-01'  -- Recent invoices
ORDER BY i.DateIssued DESC;

-- Step 5: Check if any invoices still have null CustomerId
SELECT 
    COUNT(*) as RemainingBrokenInvoices
FROM Invoices
WHERE CustomerId IS NULL;

-- Optional: Delete test invoices that cannot be fixed (if no matching customer exists)
-- UNCOMMENT ONLY IF YOU WANT TO DELETE UNFIXABLE INVOICES
/*
DELETE FROM Invoices
WHERE CustomerId IS NULL 
  AND CustomerName IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM Customers c 
      WHERE c.Code = CASE 
          WHEN CHARINDEX(' - ', Invoices.CustomerName) > 0 
          THEN LEFT(Invoices.CustomerName, CHARINDEX(' - ', Invoices.CustomerName) - 1)
          ELSE Invoices.CustomerName
      END
  );
*/

-- Step 6: Delete specific test invoices by invoice number (UNCOMMENT TO USE)
/*
DELETE FROM Invoices 
WHERE InvoiceNumber IN ('INV-20260209-682', 'INV-20260209-657', 'INV-20260209-004', 'INV-20260209-606');
*/
