# Update-AppPermissions.ps1
# Adds delegated permissions and redirect URIs to existing App Registration

$ErrorActionPreference = "Stop"
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
$clientId = $config['App']['ClientId']
$staticWebAppUrl = $config['StaticWebApp']['StaticWebAppUrl']

if (-not $clientId) {
    Write-Host "✗ No App Registration found in config. Run script 02 first." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Updating App Registration Permissions" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "App ID: $clientId" -ForegroundColor Gray
Write-Host ""

# Add delegated permissions
Write-Host "Adding delegated permissions..." -ForegroundColor Yellow
$graphId = "00000003-0000-0000-c000-000000000000"  # Microsoft Graph

# Permission IDs
$userRead = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"      # User.Read
$openid = "37f7f235-527c-4136-accd-4a02d197296e"        # openid
$profile = "14dad69e-099b-42c9-810b-d002981feec1"       # profile  
$email = "64a6cdd6-aab1-4aaf-94b8-3cc8405e90d0"         # email

Write-Host "  Adding User.Read..." -ForegroundColor Gray
az ad app permission add --id $clientId --api $graphId --api-permissions "$userRead=Scope" --only-show-errors 2>$null

Write-Host "  Adding openid..." -ForegroundColor Gray
az ad app permission add --id $clientId --api $graphId --api-permissions "$openid=Scope" --only-show-errors 2>$null

Write-Host "  Adding profile..." -ForegroundColor Gray
az ad app permission add --id $clientId --api $graphId --api-permissions "$profile=Scope" --only-show-errors 2>$null

Write-Host "  Adding email..." -ForegroundColor Gray
az ad app permission add --id $clientId --api $graphId --api-permissions "$email=Scope" --only-show-errors 2>$null

Write-Host "  ✓ Delegated permissions added" -ForegroundColor Green
Write-Host ""

# Configure redirect URIs
if ($staticWebAppUrl) {
    Write-Host "Configuring redirect URIs..." -ForegroundColor Yellow
    Write-Host "  Getting application object ID..." -ForegroundColor Gray
    $objectId = az ad app show --id $clientId --query id -o tsv
    Write-Host "  Adding SPA redirect URIs..." -ForegroundColor Gray
    $spaRedirectUris = @(
        "http://localhost:5173",
        "$staticWebAppUrl",
        "https://hub.kemponline.co.uk"
    )
    
    $body = @{
        spa = @{
            redirectUris = $spaRedirectUris
        }
        web = @{
            implicitGrantSettings = @{
                enableIdTokenIssuance = $true
            }
        }
    } | ConvertTo-Json -Depth 10 | Out-File -FilePath "$env:TEMP\spa-config.json" -Encoding utf8
    
    az rest --method PATCH --uri "https://graph.microsoft.com/v1.0/applications/$objectId" --headers "Content-Type=application/json" --body "@$env:TEMP\spa-config.json" --only-show-errors
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   SPA redirect URIs configured" -ForegroundColor Green
    } else {
        Write-Host "   Failed to configure SPA redirect URIs" -ForegroundColor Red
    }
} else {
    Write-Host " Static Web App URL not configured yet" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Update Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Opening Azure Portal to grant admin consent..." -ForegroundColor Yellow
$consentUrl = "https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/CallAnAPI/appId/$clientId"
Start-Process $consentUrl
Write-Host ""
Write-Host "Please grant admin consent in the browser window." -ForegroundColor Cyan
Write-Host "Click 'Grant admin consent for [Your Organization]'" -ForegroundColor Gray
Write-Host ""

