<#
    Check-Prerequisites.ps1
    Checks if all required tools are installed for FinanceHub deployment
#>

Write-Host ""
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "  FinanceHub - Prerequisites Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$allGood = $true

# Check .NET SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $dotnetVersion = dotnet --version
    Write-Host "  ✓ .NET SDK installed: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "  ✗ .NET SDK not found" -ForegroundColor Red
    Write-Host "    Install: winget install Microsoft.DotNet.SDK.10" -ForegroundColor Gray
    $allGood = $false
}

# Check Node.js/npm
Write-Host "Checking Node.js/npm..." -ForegroundColor Yellow
if (Get-Command node -ErrorAction SilentlyContinue) {
    $nodeVersion = node --version
    Write-Host "  ✓ Node.js installed: $nodeVersion" -ForegroundColor Green
    
    if (Get-Command npm -ErrorAction SilentlyContinue) {
        $npmVersion = npm --version
        Write-Host "  ✓ npm installed: $npmVersion" -ForegroundColor Green
    } else {
        Write-Host "  ✗ npm not found" -ForegroundColor Red
        $allGood = $false
    }
} else {
    Write-Host "  ✗ Node.js not found" -ForegroundColor Red
    Write-Host "    Install: winget install OpenJS.NodeJS.LTS" -ForegroundColor Gray
    $allGood = $false
}

# Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
if (Get-Command az -ErrorAction SilentlyContinue) {
    $azVersion = az version --query '\"azure-cli\"' -o tsv
    Write-Host "  ✓ Azure CLI installed: $azVersion" -ForegroundColor Green
    
    # Check Azure login
    try {
        $account = az account show 2>$null | ConvertFrom-Json
        Write-Host "  ✓ Logged into Azure as: $($account.user.name)" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠ Not logged into Azure" -ForegroundColor Yellow
        Write-Host "    Run: az login" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ Azure CLI not found" -ForegroundColor Red
    Write-Host "    Install: winget install Microsoft.AzureCLI" -ForegroundColor Gray
    $allGood = $false
}

# Check PowerShell version
Write-Host "Checking PowerShell..." -ForegroundColor Yellow
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -ge 7) {
    Write-Host "  ✓ PowerShell $psVersion" -ForegroundColor Green
} else {
    Write-Host "  ⚠ PowerShell $psVersion (PowerShell 7+ recommended)" -ForegroundColor Yellow
    Write-Host "    Install: winget install Microsoft.PowerShell" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($allGood) {
    Write-Host "  ✓ All prerequisites installed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Run scripts 01-07 to generate code" -ForegroundColor White
    Write-Host "  2. Run Deploy-FinanceHub.ps1 to deploy" -ForegroundColor White
} else {
    Write-Host "  ✗ Missing prerequisites" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install missing tools above, then restart PowerShell" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
