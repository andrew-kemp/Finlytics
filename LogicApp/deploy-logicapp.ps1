$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "..\finance-hub-config.ini"
$workflowFile = Join-Path $scriptRoot "workflow.json"

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
$logicAppName = $config['Azure']['LogicAppName']
$location = $config['Azure']['Location']

Write-Host "Deploying Logic App workflow: $logicAppName..." -ForegroundColor Yellow

# Wrap workflow.json in definition key
$workflow = Get-Content $workflowFile -Raw | ConvertFrom-Json
$wrappedWorkflow = @{ definition = $workflow } | ConvertTo-Json -Depth 100
$wrappedWorkflow | Set-Content "$PSScriptRoot\workflow-wrapped.json"
$wrappedFile = Join-Path $scriptRoot "workflow-wrapped.json"

az logic workflow create `
  --resource-group $resourceGroup `
  --location $location `
  --name $logicAppName `
  --definition "@$wrappedFile"

if ($LASTEXITCODE -eq 0) {
    Write-Host " Logic App workflow deployed" -ForegroundColor Green
} else {
    Write-Host " Logic App deployment failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Next: Configure Office 365 connection in Azure Portal"
Write-Host ""
