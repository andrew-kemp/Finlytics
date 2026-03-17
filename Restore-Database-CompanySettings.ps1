# Restore Azure SQL Database to recover deleted CompanySettings data
# This will restore the database to a point in time before the data was lost

$resourceGroup = "rg-financehub-kemponline"
$serverName = "financehub-sql-kemponline"
$originalDatabase = "financehub"
$restoredDatabase = "financehub-restored-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "=== DATABASE RESTORE FOR COMPANYSETTINGS DATA RECOVERY ===" -ForegroundColor Cyan
Write-Host ""

# Get database info
$dbInfo = az sql db show --name $originalDatabase --server $serverName --resource-group $resourceGroup --query "{EarliestRestore:earliestRestoreDate, Created:creationDate}" -o json | ConvertFrom-Json

Write-Host "Database Information:" -ForegroundColor Yellow
Write-Host "  Original Database: $originalDatabase"
Write-Host "  Earliest Restore Point: $($dbInfo.EarliestRestore)"
Write-Host "  Database Created: $($dbInfo.Created)"
Write-Host ""

# Calculate restore point (24 hours ago)
$restorePoint = (Get-Date).AddHours(-24).ToString("yyyy-MM-ddTHH:mm:ss")
$earliestRestore = [DateTime]::Parse($dbInfo.EarliestRestore)

if ((Get-Date $restorePoint) -lt $earliestRestore) {
    Write-Host "⚠️  WARNING: Requested restore point ($restorePoint) is earlier than earliest available ($earliestRestore)" -ForegroundColor Red
    $restorePoint = $earliestRestore.ToString("yyyy-MM-ddTHH:mm:ss")
    Write-Host "Using earliest available restore point instead: $restorePoint" -ForegroundColor Yellow
}

Write-Host "Proposed Restore:" -ForegroundColor Green
Write-Host "  Restore Point: $restorePoint"
Write-Host "  New Database Name: $restoredDatabase"
Write-Host "  Original Database: $originalDatabase (will remain unchanged)"
Write-Host ""

$confirm = Read-Host "Do you want to proceed with the restore? (yes/no)"

if ($confirm -ne "yes") {
    Write-Host "Restore cancelled." -ForegroundColor Yellow
    exit
}

Write-Host ""
Write-Host "Starting database restore..." -ForegroundColor Cyan
Write-Host "This may take 10-30 minutes depending on database size." -ForegroundColor Yellow
Write-Host ""

# Perform restore
az sql db restore `
    --dest-name $restoredDatabase `
    --resource-group $resourceGroup `
    --server $serverName `
    --name $originalDatabase `
    --time $restorePoint `
    --verbose

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Database restored successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Query the restored database to verify CompanySettings data exists"
    Write-Host "2. If data is found, copy it back to the production database"
    Write-Host "3. Delete the restored database once data is recovered to avoid costs"
    Write-Host ""
    Write-Host "To query the restored database CompanySettings:"
    Write-Host "  SELECT * FROM CompanySettings WHERE Id = 1" -ForegroundColor Gray
}
else {
    Write-Host ""
    Write-Host "❌ Database restore failed!" -ForegroundColor Red
    Write-Host "Check the error messages above for details." -ForegroundColor Yellow
}
