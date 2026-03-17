# Enable Attachments on Ledger List
$ErrorActionPreference = "Stop"

# Load configuration
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

function Get-IniContent {
    param([string]$Path)
    $ini = @{}
    if (-not (Test-Path $Path)) { return $ini }
    $section = $null
    switch -regex -file $Path {
        '^\s*\[(.+)\]\s*$' {
            $section = $matches[1]
            if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} }
        }
        '^\s*([^=]+?)\s*=\s*(.*)$' {
            if (-not $section) { $section = "Default"; if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} } }
            $name = $matches[1].Trim()
            $value = $matches[2]
            $ini[$section][$name] = $value
        }
    }
    return $ini
}

$config = Get-IniContent -Path $configFile
$siteUrl = $config['SharePoint']['SiteUrl']

Write-Host "Connecting to SharePoint site: $siteUrl" -ForegroundColor Yellow

# Connect to SharePoint using PnP PowerShell
try {
    Connect-PnPOnline -Url $siteUrl -Interactive
    Write-Host "Connected successfully" -ForegroundColor Green
} catch {
    Write-Host "Failed to connect: $_" -ForegroundColor Red
    exit 1
}

# Enable attachments on Ledger list
try {
    Write-Host "Enabling attachments on Ledger list..." -ForegroundColor Yellow
    
    $list = Get-PnPList -Identity "Ledger"
    Set-PnPList -Identity "Ledger" -EnableAttachments $true
    
    Write-Host "✓ Attachments enabled on Ledger list" -ForegroundColor Green
    
    # Verify the setting
    $list = Get-PnPList -Identity "Ledger"
    Write-Host "Attachments enabled: $($list.EnableAttachments)" -ForegroundColor Cyan
    
} catch {
    Write-Host "✗ Error enabling attachments: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Script completed successfully!" -ForegroundColor Green
