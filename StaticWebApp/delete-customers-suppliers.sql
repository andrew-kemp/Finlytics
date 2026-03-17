-- Delete all customers
DELETE FROM Customers;

-- Delete all suppliers/payees
DELETE FROM Suppliers;

-- Verify deletion
SELECT COUNT(*) AS CustomerCount FROM Customers;
SELECT COUNT(*) AS SupplierCount FROM Suppliers;
