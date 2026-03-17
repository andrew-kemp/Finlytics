# Enable Attachments on Ledger List using REST API
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
$functionAppUrl = $config['FunctionApp']['FunctionAppUrl']

Write-Host "Site URL: $siteUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "To enable attachments on the Ledger list, please:" -ForegroundColor Yellow
Write-Host "1. Go to: $siteUrl/Lists/Ledger/AllItems.aspx" -ForegroundColor White
Write-Host "2. Click the gear icon (⚙️) in the top right" -ForegroundColor White
Write-Host "3. Select 'List settings'" -ForegroundColor White
Write-Host "4. Under 'General Settings', click 'Advanced settings'" -ForegroundColor White
Write-Host "5. Under 'Attachments', select 'Enabled'" -ForegroundColor White
Write-Host "6. Click 'OK' to save" -ForegroundColor White
Write-Host ""
Write-Host "Once enabled, try uploading a receipt again." -ForegroundColor Green
