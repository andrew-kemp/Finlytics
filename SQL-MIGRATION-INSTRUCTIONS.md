# SQL Migration Instructions

## Running the DLA Classification Migration

Since the Azure CLI has permission issues, the easiest way to run the migration is through the Azure Portal:

### Option 1: Azure Portal Query Editor (Recommended)

1. Open Azure Portal: https://portal.azure.com
2. Navigate to: SQL databases → financehub
3. Click "Query editor" in the left menu
4. Authenticate with your Azure credentials
5. Copy and paste the contents of `Apply-DLA-Migration.sql`
6. Click "Run"
7. Verify the output shows:
   - "Added ClassificationSource column to DlaEntries" (or already exists message)
   - "Added IncorporationDate column to CompanySettings" (or already exists message)
   - "Migration completed successfully"

### Option 2: SQL Server Management Studio (SSMS)

1. Open SSMS
2. Connect to: financehub-sql-kemponline.database.windows.net
3. Database: financehub
4. Open `Apply-DLA-Migration.sql`
5. Execute the script

### What the Migration Does

1. Adds `ClassificationSource` column to DlaEntries table
2. Adds `IncorporationDate` column to CompanySettings table
3. Backfills existing DLA entries with `ClassificationSource = 'manual'`

### After Migration

1. Set your company incorporation date in Settings
2. New DLA entries will automatically classify as startup costs if dated before incorporation
3. Existing entries remain unchanged (manual classification)

### Migration File Location
`Apply-DLA-Migration.sql` in the root of the FinanceHub folder
