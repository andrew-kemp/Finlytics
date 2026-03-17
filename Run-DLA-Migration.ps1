#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs DLA Installment Payment database migrations
.DESCRIPTION
    Automated database migration script for DLA payment tracking features.
    Can be integrated into CI/CD pipelines. Uses SqlServer PowerShell module for reliability.
.PARAMETER ServerName
    Azure SQL Server name (default: financehub-sql-kemponline)
.PARAMETER DatabaseName
    Database name (default: FinanceHubDB)
.PARAMETER UseAzureCLI
    Use Azure CLI instead of SqlServer module (default: false)
#>

param(
    [string]$ServerName = "financehub-sql-kemponline.database.windows.net",
    [string]$DatabaseName = "FinanceHubDB",
    [switch]$UseAzureCLI = $false
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "DLA Installment Payment Migration" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Check if SqlServer module is available
if (-not $UseAzureCLI) {
    if (-not (Get-Module -ListAvailable -Name SqlServer)) {
        Write-Host "SqlServer module not found. Installing..." -ForegroundColor Yellow
        try {
            Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser -ErrorAction Stop
            Write-Host "✓ SqlServer module installed" -ForegroundColor Green
        } catch {
            Write-Host "⚠ Could not install SqlServer module. Falling back to Azure CLI..." -ForegroundColor Yellow
            $UseAzureCLI = $true
        }
    }
}

if ($UseAzureCLI) {
    # Fix Azure CLI permission issue if it exists
    Write-Host "Using Azure CLI for migrations..." -ForegroundColor Yellow
    $extensionPath = "$env:USERPROFILE\.azure\cliextensions\desktopvirtualization"
    if (Test-Path $extensionPath) {
        Write-Host "Fixing corrupted extension..." -ForegroundColor Yellow
        try {
            Get-ChildItem -Path $extensionPath -Recurse -ErrorAction SilentlyContinue | ForEach-Object { 
                try { $_.IsReadOnly = $false } catch {}
            }
            Remove-Item -Path $extensionPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "✓ Extension issue resolved" -ForegroundColor Green
        } catch {
            Write-Host "⚠ Could not remove extension" -ForegroundColor Yellow
        }
    }
    $ResourceGroup = "rg-financehub-kemponline"
    $ServerName = $ServerName -replace '\.database\.windows\.net$', ''
} else {
    Write-Host "Using SqlServer PowerShell module for migrations..." -ForegroundColor Green
    # Get Azure AD token for authentication
    Write-Host "Getting Azure AD authentication token..." -ForegroundColor Yellow
    try {
        $token = (az account get-access-token --resource https://database.windows.net --query accessToken -o tsv)
        if (-not $token) {
            throw "Failed to get access token"
        }
        Write-Host "✓ Authentication successful" -ForegroundColor Green
    } catch {
        Write-Host "✗ Could not get Azure AD token. Please run 'az login' first." -ForegroundColor Red
        exit 1
    }
}

# Define migration statements
$migrations = @(
    @{
        Name = "Add Director field to DlaEntries"
        SQL = "ALTER TABLE DlaEntries ADD Director NVARCHAR(255) NOT NULL DEFAULT '';"
    },
    @{
        Name = "Add PayInInstallments field to DlaEntries"
        SQL = "ALTER TABLE DlaEntries ADD PayInInstallments BIT NOT NULL DEFAULT 0;"
    },
    @{
        Name = "Add AmountPaid field to DlaEntries"
        SQL = "ALTER TABLE DlaEntries ADD AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0;"
    },
    @{
        Name = "Add RemainingBalance field to DlaEntries"
        SQL = "ALTER TABLE DlaEntries ADD RemainingBalance DECIMAL(18,2) NOT NULL DEFAULT 0;"
    },
    @{
        Name = "Add DlaReference field to CompanyLedgerEntries"
        SQL = "ALTER TABLE CompanyLedgerEntries ADD DlaReference NVARCHAR(50) NULL;"
    },
    @{
        Name = "Add IsFullPayment field to CompanyLedgerEntries"
        SQL = "ALTER TABLE CompanyLedgerEntries ADD IsFullPayment BIT NULL;"
    },
    @{
        Name = "Update existing DLA entries with remaining balance"
        SQL = "UPDATE DlaEntries SET RemainingBalance = AmountGross WHERE RemainingBalance = 0;"
    }
)

Write-Host ""
Write-Host "Running migrations against:" -ForegroundColor Cyan
Write-Host "  Server: $ServerName" -ForegroundColor White
Write-Host "  Database: $DatabaseName" -ForegroundColor White
if ($UseAzureCLI) {
    Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
}
Write-Host ""

$successCount = 0
$failCount = 0
$skippedCount = 0

foreach ($migration in $migrations) {
    Write-Host "[$($successCount + $failCount + $skippedCount + 1)/$($migrations.Count)] " -NoNewline -ForegroundColor Gray
    Write-Host "$($migration.Name)..." -NoNewline
    
    try {
        if ($UseAzureCLI) {
            # Use Azure CLI
            $result = az sql db query `
                -s $ServerName `
                -d $DatabaseName `
                -g $ResourceGroup `
                -q $migration.SQL `
                2>&1
            
            $success = $LASTEXITCODE -eq 0
            $errorText = $result | Out-String
        } else {
            # Use SqlServer module with Azure AD token
            try {
                Invoke-Sqlcmd `
                    -ServerInstance $ServerName `
                    -Database $DatabaseName `
                    -AccessToken $token `
                    -Query $migration.SQL `
                    -ErrorAction Stop `
                    -QueryTimeout 30 | Out-Null
                $success = $true
                $errorText = ""
            } catch {
                $success = $false
                $errorText = $_.Exception.Message
            }
        }
        
        if ($success) {
            Write-Host " ✓" -ForegroundColor Green
            $successCount++
        } else {
            # Check if it's just a "column already exists" error
            if ($errorText -match "already exists|already an object|There is already an object") {
                Write-Host " ⊘ (already exists)" -ForegroundColor Yellow
                $skippedCount++
            } else {
                Write-Host " ✗" -ForegroundColor Red
                Write-Host "  Error: $errorText" -ForegroundColor Red
                $failCount++
            }
        }
    } catch {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  Error: $_" -ForegroundColor Red
        $failCount++
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
    exit 0
}
