-- Migrate old field names to new field names in CompanySettings table
-- This copies data from legacy columns to new standardized columns

UPDATE CompanySettings
SET 
    CompanyAddress = COALESCE(CompanyAddress, Address),
    CompanyPhone = COALESCE(CompanyPhone, PhoneNumber),
    CompanyEmail = COALESCE(CompanyEmail, Email),
    VatRegistrationNumber = COALESCE(VatRegistrationNumber, VATNumber),
    BankAccountNumber = COALESCE(BankAccountNumber, AccountNumber),
    BankSortCode = COALESCE(BankSortCode, SortCode),
    InvoiceFooterText = COALESCE(InvoiceFooterText, FooterText, PaymentTerms)
WHERE Id = 1;

-- Display the results
SELECT 
    Id,
    CompanyName,
    Address as 'Old_Address',
    CompanyAddress as 'New_CompanyAddress',
    PhoneNumber as 'Old_PhoneNumber',
    CompanyPhone as 'New_CompanyPhone',
    Email as 'Old_Email',
    CompanyEmail as 'New_CompanyEmail',
    AccountNumber as 'Old_AccountNumber',
    BankAccountNumber as 'New_BankAccountNumber',
    SortCode as 'Old_SortCode',
    BankSortCode as 'New_BankSortCode',
    VATNumber as 'Old_VATNumber',
    VatRegistrationNumber as 'New_VatRegistrationNumber',
    FooterText as 'Old_FooterText',
    InvoiceFooterText as 'New_InvoiceFooterText',
    PaymentTerms as 'Old_PaymentTerms'
FROM CompanySettings
WHERE Id = 1;
