# Run migration and backfill script on Azure SQL Database
# This script applies pending EF migrations and backfills existing DLA entries

$resourceGroup = "rg-financehub-kemponline"
$serverName = "financehub-sql-kemponline"
$databaseName = "financehub"

Write-Host "Retrieving database connection information..." -ForegroundColor Cyan

# Get connection string from Azure
$connectionString = az sql db show-connection-string `
    --server $serverName `
    --name $databaseName `
    --client ado.net `
    --output tsv

Write-Host "Connection string retrieved." -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: Migration Steps" -ForegroundColor Yellow
Write-Host "1. The EF migration '20260226093506_AddDlaClassificationAndIncorporationDate' needs to be applied" -ForegroundColor White
Write-Host "2. Run the backfill script: Backfill-DLA-Classification.sql" -ForegroundColor White
Write-Host ""
Write-Host "Option 1: Apply via Function App startup (automatic)" -ForegroundColor Cyan
Write-Host "  - The migration will auto-apply when the Function App restarts" -ForegroundColor White
Write-Host ""
Write-Host "Option 2: Apply manually via Azure SQL Query Editor" -ForegroundColor Cyan
Write-Host "  1. Go to Azure Portal > SQL Database > financehub" -ForegroundColor White
Write-Host "  2. Open Query Editor and run the migration SQL:" -ForegroundColor White
Write-Host ""
Write-Host "     ALTER TABLE [DlaEntries] ADD [ClassificationSource] nvarchar(max) NULL;" -ForegroundColor Gray
Write-Host "     ALTER TABLE [CompanySettings] ADD [IncorporationDate] datetime2 NULL;" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Then run: Backfill-DLA-Classification.sql" -ForegroundColor White
Write-Host ""
Write-Host "Migration file location: FunctionApp\Migrations\20260226093506_AddDlaClassificationAndIncorporationDate.cs" -ForegroundColor DarkGray
