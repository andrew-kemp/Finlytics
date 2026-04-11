<#
    Deploy-FinanceHub.ps1
    Orchestrates deployment of Function App, Static Web App, and Logic App
#>

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  FinanceHub - Deploy All Components" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Load deployment config for expected tenant/subscription
$configPath = Join-Path $scriptRoot "deployment-config-kemponline.json"
if (Test-Path $configPath) {
    $deployConfig = Get-Content $configPath -Raw | ConvertFrom-Json
    $expectedTenantId = $deployConfig.TenantId
    $expectedSubscriptionId = $deployConfig.SubscriptionId
} else {
    Write-Host "  ⚠ No deployment-config-kemponline.json found, skipping tenant/subscription validation" -ForegroundColor Yellow
    $expectedTenantId = $null
    $expectedSubscriptionId = $null
}

# Check if user is logged into Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "  ✗ Not logged into Azure" -ForegroundColor Red
    if ($expectedTenantId) {
        Write-Host "  Logging in to tenant $expectedTenantId..." -ForegroundColor Yellow
        az login --tenant $expectedTenantId
        $account = az account show | ConvertFrom-Json
    } else {
        Write-Host "  Please run: az login" -ForegroundColor Yellow
        exit 1
    }
}

# Verify tenant
if ($expectedTenantId -and $account.tenantId -ne $expectedTenantId) {
    Write-Host "  ⚠ Wrong tenant: $($account.tenantId)" -ForegroundColor Yellow
    Write-Host "  Expected tenant: $expectedTenantId" -ForegroundColor Yellow
    Write-Host "  Switching to correct tenant..." -ForegroundColor Yellow
    az login --tenant $expectedTenantId
    $account = az account show | ConvertFrom-Json
    if ($account.tenantId -ne $expectedTenantId) {
        Write-Host "  ✗ Failed to switch to correct tenant" -ForegroundColor Red
        exit 1
    }
}

# Verify subscription
if ($expectedSubscriptionId -and $account.id -ne $expectedSubscriptionId) {
    Write-Host "  ⚠ Wrong subscription: $($account.name) ($($account.id))" -ForegroundColor Yellow
    Write-Host "  Switching to subscription $expectedSubscriptionId..." -ForegroundColor Yellow
    az account set --subscription $expectedSubscriptionId
    $account = az account show | ConvertFrom-Json
}

Write-Host "  ✓ Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "  ✓ Tenant: $($account.tenantId)" -ForegroundColor Green
Write-Host "  ✓ Subscription: $($account.name) ($($account.id))" -ForegroundColor Green
Write-Host ""

# ===========================
# 1. Deploy Function App
# ===========================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Step 1: Deploy Function App" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$functionDir = Join-Path $scriptRoot "FunctionApp"
$functionDeployScript = Join-Path $functionDir "deploy-function.ps1"

if (Test-Path $functionDeployScript) {
    Push-Location $functionDir
    try {
        & $functionDeployScript
        Write-Host ""
        Write-Host "  ✓ Function App deployed successfully" -ForegroundColor Green
        Write-Host ""
    } catch {
        Write-Host ""
        Write-Host "  ✗ Function App deployment failed: $_" -ForegroundColor Red
        Write-Host ""
        Pop-Location
        exit 1
    }
    Pop-Location
} else {
    Write-Host "  ⚠ Function App deployment script not found" -ForegroundColor Yellow
    Write-Host "  Skipping Function App deployment" -ForegroundColor Gray
    Write-Host ""
}

# ===========================
# 2. Deploy Static Web App
# ===========================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Step 2: Deploy Static Web App" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$staticWebAppDir = Join-Path $scriptRoot "StaticWebApp"
$swaDeployScript = Join-Path $staticWebAppDir "deploy-staticwebapp.ps1"

if (Test-Path $swaDeployScript) {
    Push-Location $staticWebAppDir
    try {
        & $swaDeployScript
        Write-Host ""
        Write-Host "  ✓ Static Web App deployed successfully" -ForegroundColor Green
        Write-Host ""
    } catch {
        Write-Host ""
        Write-Host "  ✗ Static Web App deployment failed: $_" -ForegroundColor Red
        Write-Host ""
        Pop-Location
        exit 1
    }
    Pop-Location
} else {
    Write-Host "  ⚠ Static Web App deployment script not found" -ForegroundColor Yellow
    Write-Host "  Skipping Static Web App deployment" -ForegroundColor Gray
    Write-Host ""
}

# ===========================
# 3. Deploy Logic App
# ===========================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Step 3: Deploy Logic App Workflow" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$logicAppDir = Join-Path $scriptRoot "LogicApp"
$logicAppDeployScript = Join-Path $logicAppDir "deploy-logicapp.ps1"

if (Test-Path $logicAppDeployScript) {
    Push-Location $logicAppDir
    try {
        & $logicAppDeployScript
        Write-Host ""
        Write-Host "  ✓ Logic App deployed successfully" -ForegroundColor Green
        Write-Host ""
    } catch {
        Write-Host ""
        Write-Host "  ✗ Logic App deployment failed: $_" -ForegroundColor Red
        Write-Host ""
        Pop-Location
        exit 1
    }
    Pop-Location
} else {
    Write-Host "  ⚠ Logic App deployment script not found" -ForegroundColor Yellow
    Write-Host "  Skipping Logic App deployment" -ForegroundColor Gray
    Write-Host ""
}

# ===========================
# Deployment Complete
# ===========================
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  All Deployments Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Post-Deployment Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Configure Logic App Office 365 Connection:" -ForegroundColor White
Write-Host "   - Open Azure Portal" -ForegroundColor Gray
Write-Host "   - Navigate to your Logic App" -ForegroundColor Gray
Write-Host "   - Open Logic App Designer" -ForegroundColor Gray
Write-Host "   - Authorize Office 365 connector" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Test Function App endpoints:" -ForegroundColor White
Write-Host "   - Test /api/GetCustomers" -ForegroundColor Gray
Write-Host "   - Test /api/GenerateCode" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Test Static Web App:" -ForegroundColor White
Write-Host "   - Open the web app URL" -ForegroundColor Gray
Write-Host "   - Sign in with Microsoft account" -ForegroundColor Gray
Write-Host "   - Verify dashboard loads" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Enable Logic App once tested" -ForegroundColor White
Write-Host ""
