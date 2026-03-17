-- Run this script as SQL admin to grant Function App access via Managed Identity
-- Connect to database: financehub

-- Create user for Function App Managed Identity
CREATE USER [financehub-func-prod-6815] FROM EXTERNAL PROVIDER;

-- Grant necessary permissions
ALTER ROLE db_datareader ADD MEMBER [financehub-func-prod-6815];
ALTER ROLE db_datawriter ADD MEMBER [financehub-func-prod-6815];
ALTER ROLE db_ddladmin ADD MEMBER [financehub-func-prod-6815];

PRINT 'Managed Identity access granted successfully';
