<#
.SYNOPSIS
    Initialize Finance Hub database with EF Core migrations

.DESCRIPTION
    Creates initial database migration and optionally applies it.
    Run this after modifying the DbContext or models.

.PARAMETER Apply
    If specified, applies the migration to the database immediately

.EXAMPLE
    .\Initialize-Database.ps1 -Apply
#>

param(
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$functionAppPath = Join-Path $scriptRoot "FunctionApp"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Finance Hub - Database Initialization" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet ef is installed
Write-Host "Checking for Entity Framework Core tools..." -ForegroundColor Yellow
try {
    $efVersion = dotnet ef --version 2>&1
    Write-Host "✓ EF Core tools found: $efVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ EF Core tools not found. Installing..." -ForegroundColor Red
    dotnet tool install --global dotnet-ef
    Write-Host "✓ EF Core tools installed" -ForegroundColor Green
}

# Navigate to Function App directory
Push-Location $functionAppPath

try {
    Write-Host ""
    Write-Host "Creating initial migration..." -ForegroundColor Yellow
    
    # Check if migrations already exist
    $migrationsFolder = Join-Path $functionAppPath "Migrations"
    if (Test-Path $migrationsFolder) {
        Write-Host "⚠ Migrations folder already exists" -ForegroundColor Yellow
        $response = Read-Host "Do you want to create a new migration? (Y/N)"
        if ($response -ne 'Y') {
            Write-Host "Cancelled" -ForegroundColor Yellow
            Pop-Location
            return
        }
    }

    # Create migration
    $migrationName = "InitialCreate_$(Get-Date -Format 'yyyyMMddHHmmss')"
    Write-Host "Migration name: $migrationName" -ForegroundColor Cyan
    
    dotnet ef migrations add $migrationName
    
    Write-Host ""
    Write-Host "✓ Migration created successfully" -ForegroundColor Green
    Write-Host ""
    
    # List migrations
    Write-Host "Current migrations:" -ForegroundColor Yellow
    dotnet ef migrations list
    Write-Host ""

    if ($Apply) {
        Write-Host "Applying migration to database..." -ForegroundColor Yellow
        dotnet ef database update
        Write-Host "✓ Migration applied successfully" -ForegroundColor Green
    } else {
        Write-Host "Migration created but NOT applied." -ForegroundColor Yellow
        Write-Host "To apply the migration:" -ForegroundColor Cyan
        Write-Host "  1. Local: dotnet ef database update" -ForegroundColor Cyan
        Write-Host "  2. Azure: Deploy Function App (migrations auto-apply on startup)" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host " Next Steps:" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host "1. Review migration files in Migrations folder" -ForegroundColor White
    Write-Host "2. Test locally: func start" -ForegroundColor White
    Write-Host "3. Deploy to Azure: .\Deploy-FinanceHub-Azure.ps1" -ForegroundColor White
    Write-Host ""

} catch {
    Write-Host "✗ Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "  1. Ensure .NET 8 SDK is installed" -ForegroundColor White
    Write-Host "  2. Check connection string in local.settings.json" -ForegroundColor White
    Write-Host "  3. Verify SQL Server is running (LocalDB/Express/Azure)" -ForegroundColor White
    Pop-Location
    exit 1
}

Pop-Location
