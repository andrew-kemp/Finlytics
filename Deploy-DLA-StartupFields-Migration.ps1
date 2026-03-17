#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs DLA startup fields database migration
.DESCRIPTION
    Executes the Add-DlaEntry-StartupFields.sql migration using SQL connection string from Key Vault.
.PARAMETER KeyVaultName
    Key Vault name (default: financehub-kv-kemponline)
.PARAMETER SecretName
    Connection string secret name (default: SqlConnectionString)
#>

param(
    [string]$KeyVaultName = "fh-kv-kemponline",
    [string]$SecretName = "SqlConnectionString",
    [switch]$UseAad
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "DLA Startup Fields Migration" -ForegroundColor Cyan
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
    $authMode = $csBuilder["Authentication"]
} catch {
    Write-Host "✗ Could not parse connection string" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Migration target:" -ForegroundColor Cyan
Write-Host "  Server: $server" -ForegroundColor White
Write-Host "  Database: $database" -ForegroundColor White
Write-Host ""

$migrationFile = Join-Path $PSScriptRoot "FunctionApp\Migrations\Add-DlaEntry-StartupFields.sql"

if (-not (Test-Path $migrationFile)) {
    Write-Host "✗ Migration file not found: $migrationFile" -ForegroundColor Red
    exit 1
}

Write-Host "Applying migration: $migrationFile" -ForegroundColor Yellow
try {
    $useAadAuth = $UseAad.IsPresent -or [string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrWhiteSpace($password) -or ($authMode -match "Active Directory")
    if ($useAadAuth) {
        Write-Host "Using Azure AD access token for SQL authentication" -ForegroundColor Cyan
        $accessToken = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
        if (-not $accessToken) {
            throw "Could not acquire Azure AD access token for SQL"
        }
        Invoke-Sqlcmd `
            -ServerInstance $server `
            -Database $database `
            -AccessToken $accessToken `
            -InputFile $migrationFile `
            -ErrorAction Stop `
            -QueryTimeout 120 | Out-Null
    } else {
        Invoke-Sqlcmd `
            -ServerInstance $server `
            -Database $database `
            -Username $username `
            -Password $password `
            -InputFile $migrationFile `
            -ErrorAction Stop `
            -QueryTimeout 120 | Out-Null
    }

    Write-Host "✓ Migration applied successfully" -ForegroundColor Green
} catch {
    $errorMsg = $_.Exception.Message
    if ($errorMsg -match "already exists|There is already an object|duplicate") {
        Write-Host "⊘ Migration already applied" -ForegroundColor Yellow
    } else {
        Write-Host "✗ Migration failed" -ForegroundColor Red
        Write-Host "  $errorMsg" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Redeploy the Function App" -ForegroundColor White
Write-Host "  2. Verify DLA Startup capture in the UI" -ForegroundColor White
