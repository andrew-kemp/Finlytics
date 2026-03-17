-- Connect to the financehub database and run this script
-- This grants the Function App's Managed Identity access to the database

CREATE USER [financehub-func-2900] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [financehub-func-2900];
ALTER ROLE db_datawriter ADD MEMBER [financehub-func-2900];
ALTER ROLE db_ddladmin ADD MEMBER [financehub-func-2900];
GO
