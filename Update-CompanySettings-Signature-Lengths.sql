-- Expand signature columns to support base64 signature images
ALTER TABLE CompanySettings
    ALTER COLUMN DirectorSignature NVARCHAR(MAX) NULL;

ALTER TABLE CompanySettings
    ALTER COLUMN AuthorizedOfficerSignature NVARCHAR(MAX) NULL;
