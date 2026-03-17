# Quick Fix: Add 3 Missing Columns to Company Settings
# Uses same authentication as main provisioner

$siteUrl = "https://kempy.sharepoint.com/sites/AKFinancehubV2"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Quick Column Fix for Company Settings" -ForegroundColor Cyan  
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Read config for tenant info
if (-not (Test-Path $configFile)) {
    Write-Host "✗ Config file not found: $configFile" -ForegroundColor Red
    Write-Host "Run 02-FinanceHub-ProvisionerV2.ps1 first to create the config." -ForegroundColor Yellow
    exit 1
}

# Simple INI reader
function Get-IniValue {
    param([string]$Section, [string]$Key)
    $content = Get-Content $configFile
    $inSection = $false
    foreach ($line in $content) {
        if ($line -match "^\[$Section\]") {
            $inSection = $true
            continue
        }
        if ($inSection -and $line -match "^$Key\s*=\s*(.+)") {
            return $matches[1].Trim()
        }
        if ($line -match "^\[") {
            $inSection = $false
        }
    }
    return $null
}

try {
    $tenantId = Get-IniValue -Section "Tenant" -Key "TenantId"
    $clientId = Get-IniValue -Section "App" -Key "ClientId"
    $thumbprint = Get-IniValue -Section "App" -Key "Thumbprint"

    if (-not $tenantId -or -not $clientId -or -not $thumbprint) {
        throw "Missing configuration in $configFile. Found: Tenant=$tenantId, Client=$clientId, Thumbprint=$thumbprint"
    }

    Write-Host "Connecting to SharePoint..." -ForegroundColor Yellow
    Write-Host "  Tenant: $tenantId" -ForegroundColor Gray
    Write-Host "  App: $clientId" -ForegroundColor Gray

    Connect-PnPOnline -Url $siteUrl -ClientId $clientId -Thumbprint $thumbprint -Tenant $tenantId -ErrorAction Stop
    Write-Host "✓ Connected successfully!" -ForegroundColor Green
    Write-Host ""

    # Add missing columns
    Write-Host "Adding missing columns..." -ForegroundColor Yellow

    function Add-ColumnSafe {
        param($DisplayName, $InternalName, $Type)
        try {
            $existing = Get-PnPField -List "Company Settings" -Identity $InternalName -ErrorAction SilentlyContinue
            if ($existing) {
                Write-Host "  ℹ  $DisplayName - Already exists" -ForegroundColor Gray
            } else {
                Add-PnPField -List "Company Settings" -DisplayName $DisplayName -InternalName $InternalName -Type $Type -AddToDefaultView | Out-Null
                Write-Host "  ✓  $DisplayName - CREATED" -ForegroundColor Green
            }
        } catch {
            Write-Host "  ✗  $DisplayName - ERROR: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Add-ColumnSafe -DisplayName "SMTP Server" -InternalName "SmtpServer" -Type "Text"
    Add-ColumnSafe -DisplayName "SMTP Port" -InternalName "SmtpPort" -Type "Number"
    Add-ColumnSafe -DisplayName "Has Authorized Officer" -InternalName "HasAuthorizedOfficer" -Type "Boolean"

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "  ✓ Done! Columns added successfully" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next: Go to https://hub.kemponline.co.uk and test:" -ForegroundColor Cyan
    Write-Host "  1. Settings → SMTP Configuration → Enter server details" -ForegroundColor White
    Write-Host "  2. Settings → Digital Signatures → Draw and save signatures" -ForegroundColor White
    Write-Host "  3. Click 'Save Settings' at the bottom" -ForegroundColor White
    Write-Host "  4. Refresh page - signatures should reappear!" -ForegroundColor White
    Write-Host ""

    Disconnect-PnPOnline
}
catch {
    Write-Host ""
    Write-Host "✗ ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Try running the full provisioner instead:" -ForegroundColor Yellow
    Write-Host "  .\02-FinanceHub-ProvisionerV2.ps1" -ForegroundColor Gray
    Write-Host ""
    exit 1
}
