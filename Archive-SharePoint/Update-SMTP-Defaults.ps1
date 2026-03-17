# =======================================================================================
# Update-SMTP-Defaults.ps1
# Updates the existing Company Settings item with SMTP2GO default values
# =======================================================================================

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "  Update SMTP Default Values" -ForegroundColor Cyan
Write-Host "================================================================`n" -ForegroundColor Cyan

# Read config from INI file
function Get-IniValue {
    param(
        [string]$FilePath,
        [string]$Section,
        [string]$Key
    )
    $content = Get-Content $FilePath -Raw
    $pattern = "(?ms)^\[$Section\].*?^\s*$Key\s*=\s*(.+?)$"
    if ($content -match $pattern) {
        return $matches[1].Trim()
    }
    return $null
}

$configFile = Join-Path $PSScriptRoot "finance-hub-config.ini"
$siteUrl = Get-IniValue -FilePath $configFile -Section "SharePoint" -Key "SiteUrl"
$clientId = Get-IniValue -FilePath $configFile -Section "App" -Key "ClientId"
$thumbprint = Get-IniValue -FilePath $configFile -Section "App" -Key "Thumbprint"
$tenantId = Get-IniValue -FilePath $configFile -Section "Tenant" -Key "TenantId"

# Connect to SharePoint
Write-Host "Connecting to SharePoint..." -ForegroundColor Yellow
Write-Host "  Tenant: $tenantId" -ForegroundColor Gray
Write-Host "  App: $clientId" -ForegroundColor Gray

Connect-PnPOnline -Url $siteUrl -ClientId $clientId -Thumbprint $thumbprint -Tenant $tenantId

Write-Host "✓ Connected successfully!`n" -ForegroundColor Green

# Update the Company Settings item
Write-Host "Updating SMTP default values..." -ForegroundColor Yellow

try {
    # Get the first (and should be only) item
    $item = Get-PnPListItem -List "Company Settings" -PageSize 1 | Select-Object -First 1
    
    if ($item) {
        # Update with SMTP2GO defaults
        Set-PnPListItem -List "Company Settings" -Identity $item.Id -Values @{
            SmtpServer = "mail.smtp2go.com"
            SmtpPort = 587
        } | Out-Null
        
        Write-Host "  ✓  SMTP Server set to: mail.smtp2go.com" -ForegroundColor Green
        Write-Host "  ✓  SMTP Port set to: 587" -ForegroundColor Green
    } else {
        Write-Host "  ✗  No Company Settings item found" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ✗  Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Disconnect-PnPOnline

Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "  ✓ Done! SMTP defaults updated" -ForegroundColor Green
Write-Host "================================================================`n" -ForegroundColor Cyan

Write-Host "Next: Go to https://hub.kemponline.co.uk/settings" -ForegroundColor Yellow
Write-Host "  - SMTP Server and Port should now show the defaults" -ForegroundColor Gray
Write-Host "  - Add your SMTP Username and Password" -ForegroundColor Gray
Write-Host "  - Click 'Send Test Email' to verify" -ForegroundColor Gray
