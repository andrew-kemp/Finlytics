#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs DLA Installment Payment database migrations
.DESCRIPTION
    Automated database migration script using SQL connection string from Key Vault.
    Fully automated for CI/CD pipelines.
.PARAMETER KeyVaultName
    Key Vault name (default: financehub-kv-kemponline)
.PARAMETER SecretName
    Connection string secret name (default: SqlConnectionString)
#>

param(
    [string]$KeyVaultName = "financehub-kv-kemponline",
    [string]$SecretName = "SqlConnectionString"
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "DLA Installment Payment Migration" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Install SqlServer module if needed
if (-not (Get-Module -ListAvailable -Name SqlServer)) {
    Write-Host "Installing SqlServer module..." -ForegroundColor Yellow
    Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser
    Write-Host "✓ SqlServer module installed" -ForegroundColor Green
}

# Get connection string from Key Vault
Write-Host "Retrieving connection string from Key Vault..." -ForegroundColor Yellow
try {
    $connectionString = az keyvault secret show `
        --vault-name $KeyVaultName `
        --name $SecretName `
        --query value `
        -o tsv `
        2>$null
    
    if (-not $connectionString) {
        throw "Connection string is empty"
    }
    Write-Host "✓ Connection string retrieved" -ForegroundColor Green
} catch {
    Write-Host "✗ Could not retrieve connection string from Key Vault" -ForegroundColor Red
    Write-Host "  Make sure you're logged in: az login" -ForegroundColor Yellow
    exit 1
}

# Parse connection string
try {
    $csBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($connectionString)
    $server = $csBuilder.DataSource
    $database = $csBuilder.InitialCatalog
    $username = $csBuilder.UserID
    $password = $csBuilder.Password
} catch {
    Write-Host "✗ Could not parse connection string" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Migration target:" -ForegroundColor Cyan
Write-Host "  Server: $server" -ForegroundColor White
Write-Host "  Database: $database" -ForegroundColor White
Write-Host ""

# Define migrations
$migrations = @(
    @{
        Name = "Add Director field"
        SQL = "ALTER TABLE DlaEntries ADD Director NVARCHAR(255) NOT NULL DEFAULT '';"
    },
    @{
        Name = "Add PayInInstallments field"
        SQL = "ALTER TABLE DlaEntries ADD PayInInstallments BIT NOT NULL DEFAULT 0;"
    },
    @{
        Name = "Add AmountPaid field"
        SQL = "ALTER TABLE DlaEntries ADD AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0;"
    },
    @{
        Name = "Add RemainingBalance field"
        SQL = "ALTER TABLE DlaEntries ADD RemainingBalance DECIMAL(18,2) NOT NULL DEFAULT 0;"
    },
    @{
        Name = "Add DlaReference field"
        SQL = "ALTER TABLE CompanyLedgerEntries ADD DlaReference NVARCHAR(50) NULL;"
    },
    @{
        Name = "Add IsFullPayment field"
        SQL = "ALTER TABLE CompanyLedgerEntries ADD IsFullPayment BIT NULL;"
    },
    @{
        Name = "Initialize remaining balances"
        SQL = "UPDATE DlaEntries SET RemainingBalance = AmountGross WHERE RemainingBalance = 0;"
    }
)

$successCount = 0
$failCount = 0
$skippedCount = 0

foreach ($migration in $migrations) {
    $index = $successCount + $failCount + $skippedCount + 1
    Write-Host "[$index/$($migrations.Count)] " -NoNewline -ForegroundColor Gray
    Write-Host "$($migration.Name)..." -NoNewline
    
    try {
        Invoke-Sqlcmd `
            -ServerInstance $server `
            -Database $database `
            -Username $username `
            -Password $password `
            -Query $migration.SQL `
            -ErrorAction Stop `
            -QueryTimeout 30 | Out-Null
        
        Write-Host " ✓" -ForegroundColor Green
        $successCount++
    } catch {
        $errorMsg = $_.Exception.Message
        if ($errorMsg -match "already exists|There is already an object|duplicate") {
            Write-Host " ⊘ (already exists)" -ForegroundColor Yellow
            $skippedCount++
        } else {
            Write-Host " ✗" -ForegroundColor Red
            Write-Host "    $errorMsg" -ForegroundColor Red
            $failCount++
        }
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Migration Summary:" -ForegroundColor Cyan
Write-Host "  Successful: $successCount" -ForegroundColor Green
Write-Host "  Skipped: $skippedCount" -ForegroundColor Yellow
Write-Host "  Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "================================================" -ForegroundColor Cyan

if ($failCount -gt 0) {
    Write-Host ""
    Write-Host "⚠ Some migrations failed. Please review errors above." -ForegroundColor Red
    exit 1
} else {
    Write-Host ""
    Write-Host "✓ All migrations completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Test the RecordDlaPayment API endpoint" -ForegroundColor White
    Write-Host "  2. Create a new DLA entry with PayInInstallments=true" -ForegroundColor White
    Write-Host "  3. Record a payment and verify balance tracking" -ForegroundColor White
    exit 0
}
