# Deploy Function App
$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "..\finance-hub-config.ini"

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

$resourceGroup = $config['Azure']['ResourceGroup']
$functionAppName = $config['Azure']['FunctionAppName']
$storageAccount = $config['Azure']['StorageAccountName']

Write-Host "Deploying Function App: $functionAppName..." -ForegroundColor Yellow

# Build the Function App
Write-Host "Building Function App..." -ForegroundColor Gray
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    exit 1
}

# Publish to folder
Write-Host "Publishing Function App..." -ForegroundColor Gray
dotnet publish --configuration Release --output ./publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Publish failed" -ForegroundColor Red
    exit 1
}

# Create deployment package
Write-Host "Creating deployment package..." -ForegroundColor Gray
Compress-Archive -Path ./publish/* -DestinationPath ./deploy.zip -Force

# Deploy to Azure
Write-Host "Deploying to Azure Function App..." -ForegroundColor Gray
az functionapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $functionAppName `
  --src ./deploy.zip

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Function App deployed successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Function App deployment failed" -ForegroundColor Red
    exit 1
}

# Clean up
Remove-Item ./publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ./deploy.zip -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Function App URL: $($config['FunctionApp']['FunctionAppUrl'])" -ForegroundColor Cyan
Write-Host ""
