<#
.SYNOPSIS
    Complete automated deployment of FinanceHub with admin authentication

.DESCRIPTION
    This script performs a complete end-to-end deployment:
    1. Builds and deploys Function App with admin auth endpoints
    2. Runs database migrations to create admin tables
    3. Builds and deploys Static Web App with unified login
    4. Configures all necessary settings

.PARAMETER ConfigFile
    Path to deployment configuration JSON file (default: deployment-config-2900.json)

.PARAMETER SkipBuild
    Skip building the applications (use existing build outputs)

.PARAMETER SqlPassword
    SQL Server admin password (required for database migration)

.EXAMPLE
    .\Deploy-FinanceHub-Complete.ps1 -SqlPassword "YourPassword123!"
#>

[CmdletBinding()]
param(
    [string]$ConfigFile = "deployment-config-2900.json",
    [switch]$SkipBuild,
    [Parameter(Mandatory=$true)]
    [string]$SqlPassword
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  → $Message" -ForegroundColor White
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  ⚠ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "  ✗ $Message" -ForegroundColor Red
}

# ===========================
# Header
# ===========================
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  FinanceHub - Complete Automated Deployment" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# ===========================
# Check Prerequisites
# ===========================
Write-Step "Checking Prerequisites"

# Check Azure login
Write-Info "Checking Azure login..."
try {
    $account = az account show 2>$null | ConvertFrom-Json
    Write-Success "Logged in as: $($account.user.name)"
    Write-Success "Subscription: $($account.name)"
} catch {
    Write-Error "Not logged into Azure"
    Write-Host ""
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Check .NET SDK
Write-Info "Checking .NET SDK..."
try {
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK version: $dotnetVersion"
} catch {
    Write-Error ".NET SDK not found"
    exit 1
}

# Check Node.js
Write-Info "Checking Node.js..."
try {
    $nodeVersion = node --version
    Write-Success "Node.js version: $nodeVersion"
} catch {
    Write-Error "Node.js not found"
    exit 1
}

# Check Azure Functions Core Tools
Write-Info "Checking Azure Functions Core Tools..."
try {
    $funcVersion = func --version
    Write-Success "Functions Core Tools version: $funcVersion"
} catch {
    Write-Error "Azure Functions Core Tools not found"
    exit 1
}

# Check Static Web Apps CLI
Write-Info "Checking Static Web Apps CLI..."
try {
    $swaVersion = swa --version
    Write-Success "SWA CLI version: $swaVersion"
} catch {
    Write-Warning "SWA CLI not found, will use Azure CLI instead"
}

# Load configuration
Write-Info "Loading deployment configuration..."
$configPath = Join-Path $scriptRoot $ConfigFile
if (-not (Test-Path $configPath)) {
    Write-Error "Configuration file not found: $configPath"
    exit 1
}
$config = Get-Content $configPath | ConvertFrom-Json
Write-Success "Configuration loaded"

$resourceGroup = $config.ResourceGroup
$functionAppName = $config.Resources.FunctionApp
$staticWebAppName = $config.Resources.StaticWebApp
$sqlServer = $config.Resources.SqlServerFQDN
$sqlDatabase = $config.Resources.SqlDatabase
$sqlUser = $config.Resources.SqlAdminUser

Write-Host ""
Write-Info "Target Resources:"
Write-Host "    Resource Group: $resourceGroup" -ForegroundColor Gray
Write-Host "    Function App: $functionAppName" -ForegroundColor Gray
Write-Host "    Static Web App: $staticWebAppName" -ForegroundColor Gray
Write-Host "    SQL Server: $sqlServer" -ForegroundColor Gray
Write-Host "    SQL Database: $sqlDatabase" -ForegroundColor Gray

# ===========================
# Step 1: Build Function App
# ===========================
Write-Step "Step 1: Build Function App"

$functionAppDir = Join-Path $scriptRoot "FunctionApp"
Push-Location $functionAppDir

if (-not $SkipBuild) {
    Write-Info "Cleaning previous builds..."
    dotnet clean --configuration Release | Out-Null
    Write-Success "Clean complete"
    
    Write-Info "Building Function App..."
    dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        Pop-Location
        exit 1
    }
    Write-Success "Build complete"
    
    Write-Info "Publishing Function App..."
    dotnet publish --configuration Release --output ./publish
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed"
        Pop-Location
        exit 1
    }
    Write-Success "Publish complete"
} else {
    Write-Warning "Skipping build (using existing output)"
}

Pop-Location

# ===========================
# Step 2: Deploy Function App
# ===========================
Write-Step "Step 2: Deploy Function App to Azure"

Push-Location $functionAppDir

Write-Info "Deploying to $functionAppName..."
func azure functionapp publish $functionAppName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Function App deployment failed"
    Pop-Location
    exit 1
}
Write-Success "Function App deployed"

Pop-Location

# ===========================
# Step 3: Run Database Migration
# ===========================
Write-Step "Step 3: Apply Database Migrations"

Push-Location $functionAppDir

Write-Info "Configuring connection string..."
$connectionString = "Server=$sqlServer;Database=$sqlDatabase;User Id=$sqlUser;Password=$SqlPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$env:ConnectionStrings__FinanceHubDb = $connectionString
$env:DOTNET_ROLL_FORWARD = "LatestMajor"

Write-Info "Running EF migrations..."
dotnet ef database update --verbose
if ($LASTEXITCODE -ne 0) {
    Write-Error "Database migration failed"
    Write-Host ""
    Write-Warning "You may need to run the migration manually"
    Write-Host "SQL Script available at: migration-admin-tables.sql" -ForegroundColor Gray
    Pop-Location
    # Don't exit - continue with other deployments
} else {
    Write-Success "Database migrations applied successfully"
}

Pop-Location

# ===========================
# Step 4: Build Static Web App
# ===========================
Write-Step "Step 4: Build Static Web App"

$staticWebAppDir = Join-Path $scriptRoot "StaticWebApp"
Push-Location $staticWebAppDir

if (-not $SkipBuild) {
    Write-Info "Installing npm dependencies..."
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Error "npm install failed"
        Pop-Location
        exit 1
    }
    Write-Success "Dependencies installed"
    
    Write-Info "Building Static Web App..."
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        Pop-Location
        exit 1
    }
    Write-Success "Build complete"
} else {
    Write-Warning "Skipping build (using existing output)"
}

Pop-Location

# ===========================
# Step 5: Deploy Static Web App
# ===========================
Write-Step "Step 5: Deploy Static Web App to Azure"

Push-Location $staticWebAppDir

Write-Info "Deploying to $staticWebAppName..."

# Try SWA CLI first
$swaDeployed = $false
try {
    swa deploy --app-location ./dist --app-name $staticWebAppName --resource-group $resourceGroup --env production 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $swaDeployed = $true
        Write-Success "Static Web App deployed (using SWA CLI)"
    }
} catch {
    Write-Warning "SWA CLI deployment failed, trying Azure CLI..."
}

# Fallback to Azure CLI
if (-not $swaDeployed) {
    az staticwebapp deploy --name $staticWebAppName --resource-group $resourceGroup --source ./dist
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Static Web App deployment failed"
        Pop-Location
        exit 1
    }
    Write-Success "Static Web App deployed (using Azure CLI)"
}

Pop-Location

# ===========================
# Step 6: Verify Deployment
# ===========================
Write-Step "Step 6: Verify Deployment"

Write-Info "Checking Function App status..."
$functionApp = az functionapp show --name $functionAppName --resource-group $resourceGroup 2>$null | ConvertFrom-Json
if ($functionApp) {
    Write-Success "Function App: $($functionApp.defaultHostName)"
} else {
    Write-Warning "Could not verify Function App"
}

Write-Info "Checking Static Web App status..."
$staticApp = az staticwebapp show --name $staticWebAppName --resource-group $resourceGroup 2>$null | ConvertFrom-Json
if ($staticApp) {
    Write-Success "Static Web App: $($staticApp.defaultHostname)"
    if ($config.CustomDomain) {
        Write-Success "Custom Domain: $($config.CustomDomain)"
    }
} else {
    Write-Warning "Could not verify Static Web App"
}

# ===========================
# Deployment Summary
# ===========================
Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

Write-Host "URLs:" -ForegroundColor Yellow
if ($functionApp) {
    Write-Host "  Function App: https://$($functionApp.defaultHostName)" -ForegroundColor White
}
if ($staticApp) {
    Write-Host "  Static Web App: https://$($staticApp.defaultHostname)" -ForegroundColor White
    if ($config.CustomDomain) {
        Write-Host "  Custom Domain: https://$($config.CustomDomain)" -ForegroundColor White
    }
}
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Login to your application:" -ForegroundColor White
Write-Host "   • Enter 'admin' for local authentication" -ForegroundColor Gray
Write-Host "   • Default password: FinanceHub2026!" -ForegroundColor Gray
Write-Host "   • You'll be prompted to change the password" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Configure SSO (optional):" -ForegroundColor White
Write-Host "   • Navigate to Settings → Security & SSO" -ForegroundColor Gray
Write-Host "   • Create Azure App Registration" -ForegroundColor Gray
Write-Host "   • Configure Entra ID settings" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Configure SMTP for email notifications:" -ForegroundColor White
Write-Host "   • Navigate to Settings → Company" -ForegroundColor Gray
Write-Host "   • Enter SMTP server details" -ForegroundColor Gray
Write-Host ""

Write-Host "Done! 🎉" -ForegroundColor Green
Write-Host ""
