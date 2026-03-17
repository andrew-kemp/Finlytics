# Deploy Static Web App
$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "..\finance-hub-config.ini"
$fallbackConfigFile = Join-Path $scriptRoot "..\Archive-SharePoint\finance-hub-config.ini"
$jsonConfigFile = Join-Path $scriptRoot "..\deployment-config-kemponline.json"

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

$config = $null
if (Test-Path $jsonConfigFile) {
    $json = Get-Content -Raw -Path $jsonConfigFile | ConvertFrom-Json
    if ($null -ne $json -and $null -ne $json.ResourceGroup -and $null -ne $json.Resources.StaticWebApp) {
        $config = @{
            Azure = @{
                ResourceGroup  = $json.ResourceGroup
                StaticWebAppName = $json.Resources.StaticWebApp
            }
        }
    }
}

if ($null -eq $config) {
    $config = Get-IniContent -Path $configFile
    if (-not $config.ContainsKey('Azure') -or -not $config['Azure'].ContainsKey('ResourceGroup') -or -not $config['Azure'].ContainsKey('StaticWebAppName')) {
        $config = Get-IniContent -Path $fallbackConfigFile
    }
}

$resourceGroup = $config['Azure']['ResourceGroup']
$staticWebAppName = $config['Azure']['StaticWebAppName']

Write-Host "Deploying Static Web App: $staticWebAppName..." -ForegroundColor Yellow

# Check if npm is installed
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    Write-Host "✗ npm not found. Please install Node.js" -ForegroundColor Red
    exit 1
}

# Install dependencies
Write-Host "Installing dependencies..." -ForegroundColor Gray
npm install

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ npm install failed" -ForegroundColor Red
    exit 1
}

# Build the app
Write-Host "Building React app..." -ForegroundColor Gray
npm run build

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    exit 1
}

# Get deployment token
Write-Host "Getting deployment token..." -ForegroundColor Gray
$token = az staticwebapp secrets list `
  --name $staticWebAppName `
  --resource-group $resourceGroup `
  --query "properties.apiKey" `
  --output tsv

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to get deployment token" -ForegroundColor Red
    exit 1
}

# Deploy using SWA CLI
Write-Host "Deploying to Azure Static Web App..." -ForegroundColor Gray

# Check if SWA CLI is installed
if (-not (Get-Command swa -ErrorAction SilentlyContinue)) {
    Write-Host "Installing Azure Static Web Apps CLI..." -ForegroundColor Gray
    npm install -g @azure/static-web-apps-cli
}

swa deploy ./dist `
  --deployment-token $token `
  --env production

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Static Web App deployed successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Static Web App deployment failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Static Web App URL: https://hub.kemponline.co.uk" -ForegroundColor Cyan
Write-Host ""
