<#
.SYNOPSIS
    New Finance Hub Deployment to Azure with Azure SQL Database
    
.DESCRIPTION
    Deploys a complete NEW Finance Hub solution with:
    - Azure SQL Database (Serverless)
    - Azure Function App with Managed Identity
    - Static Web App with NEW Entra App Registration
    - Key Vault for secrets
    - Application Insights
    - Storage Account for documents
    - Unique resource naming with random suffix
    
.PARAMETER SubscriptionId
    Target Azure subscription ID (optional - will prompt if not provided)
    
.PARAMETER Location
    Azure region (default: uksouth)
    
.PARAMETER ResourceGroupName
    Resource group name (will add random suffix if exists)
    
.PARAMETER CustomDomain
    Custom domain for Static Web App (optional)

.EXAMPLE
    .\Deploy-FinanceHub-New.ps1
    
.EXAMPLE
    .\Deploy-FinanceHub-New.ps1 -SubscriptionId "xxx-xxx" -ResourceGroupName "rg-financehub-sqldb" -CustomDomain "finance.yourdomain.com"
#>

param(
    [string]$SubscriptionId,
    [string]$Location = "uksouth",
    [string]$ResourceGroupName,
    [string]$CustomDomain
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# ===========================
# Configuration
# ===========================
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logFile = Join-Path $scriptRoot "deployment-new-$timestamp.log"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $logMessage = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $logFile -Value $logMessage
}

function Test-AzureCLI {
    try {
        $null = az version 2>$null
        return $true
    } catch {
        return $false
    }
}

function Get-RandomSuffix {
    return Get-Random -Minimum 1000 -Maximum 9999
}

function Test-ResourceGroupExists {
    param([string]$Name)
    $result = az group exists --name $Name
    return $result -eq "true"
}

function Get-UniqueResourceGroupName {
    param([string]$BaseName)
    
    if (-not (Test-ResourceGroupExists -Name $BaseName)) {
        return $BaseName
    }
    
    Write-Log "Resource group '$BaseName' already exists, generating unique name..." "WARN"
    
    do {
        $suffix = Get-RandomSuffix
        $newName = "$BaseName-$suffix"
    } while (Test-ResourceGroupExists -Name $newName)
    
    Write-Log "Using unique resource group name: $newName" "INFO"
    return $newName
}

function Register-AzureProvider {
    param([string]$Namespace)
    
    Write-Log "Checking provider registration: $Namespace" "INFO"
    $state = az provider show --namespace $Namespace --query "registrationState" -o tsv 2>$null
    
    if ($state -ne "Registered") {
        Write-Log "Registering provider: $Namespace (this may take a few minutes)..." "INFO"
        az provider register --namespace $Namespace --wait | Out-Null
        Write-Log "Provider $Namespace registered successfully" "INFO"
    } else {
        Write-Log "Provider $Namespace already registered" "INFO"
    }
}

function Invoke-AzCommand {
    param(
        [string]$Command,
        [string]$ErrorMessage,
        [switch]$AllowFailure
    )
    
    try {
        $result = Invoke-Expression $Command 2>&1
        if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
            Write-Log "$ErrorMessage : $result" "ERROR"
            throw $ErrorMessage
        }
        return $result
    } catch {
        if (-not $AllowFailure) {
            Write-Log "$ErrorMessage : $_" "ERROR"
            throw
        }
        return $null
    }
}

# ===========================
# Main Deployment
# ===========================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Finance Hub - NEW Azure SQL Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Log "Starting NEW Finance Hub deployment to Azure" "INFO"

# Check Azure CLI
if (-not (Test-AzureCLI)) {
    Write-Log "Azure CLI not found. Please install from: https://aka.ms/installazurecliwindows" "ERROR"
    exit 1
}

# Get Azure account
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Log "Not logged in to Azure. Running 'az login'..." "INFO"
    az login
    $account = az account show | ConvertFrom-Json
}

# Set subscription
if ($SubscriptionId) {
    Write-Log "Setting subscription to: $SubscriptionId" "INFO"
    az account set --subscription $SubscriptionId
} else {
    $SubscriptionId = $account.id
}

$account = az account show | ConvertFrom-Json
$tenantId = $account.tenantId

Write-Log "Using subscription: $($account.name) ($SubscriptionId)" "INFO"
Write-Log "Tenant ID: $tenantId" "INFO"

# Get tenant domain
$tenantDomain = Read-Host "Enter Tenant Domain (e.g., kempy.onmicrosoft.com)"
while ([string]::IsNullOrWhiteSpace($tenantDomain)) {
    $tenantDomain = Read-Host "Tenant Domain is required"
}

# Get or generate resource group name
if (-not $ResourceGroupName) {
    $ResourceGroupName = Read-Host "Enter Resource Group Name [rg-financehub-sqldb]"
    if ([string]::IsNullOrWhiteSpace($ResourceGroupName)) {
        $ResourceGroupName = "rg-financehub-sqldb"
    }
}

# Get custom domain if needed
if (-not $CustomDomain) {
    $customDomainInput = Read-Host "Enter Custom Domain for Static Web App (optional, press Enter to skip)"
    if (-not [string]::IsNullOrWhiteSpace($customDomainInput)) {
        $CustomDomain = $customDomainInput
    }
}

# Generate unique RG name and suffix
$ResourceGroupName = Get-UniqueResourceGroupName -BaseName $ResourceGroupName
$suffix = if ($ResourceGroupName -match '-(\d{4})$') { $Matches[1] } else { Get-RandomSuffix }

Write-Host ""
Write-Host "Configuration Summary:" -ForegroundColor Yellow
Write-Host "  Subscription: $($account.name)" -ForegroundColor White
Write-Host "  Tenant: $tenantDomain" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host "  Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "  Resource Suffix: $suffix" -ForegroundColor White
if ($CustomDomain) {
    Write-Host "  Custom Domain: $CustomDomain" -ForegroundColor White
}
Write-Host ""

$confirm = Read-Host "Proceed with NEW deployment? (Y/N) [Y]"
if ($confirm -and $confirm -ne "Y" -and $confirm -ne "y") {
    Write-Log "Deployment cancelled by user" "INFO"
    exit 0
}

# Generate resource names
$sqlServerName = "financehub-sql-$suffix"
$sqlDbName = "financehub"
$sqlAdminUser = "sqladmin"
$sqlAdminPassword = -join ((65..90) + (97..122) + (48..57) + @(33,35,36,37,38,42,43,45,61) | Get-Random -Count 16 | ForEach-Object {[char]$_}) + "Aa1!"
$storageAccountName = "fhstor$suffix"
$functionStorageName = "fhfuncstor$suffix"
$functionAppName = "financehub-func-$suffix"
$staticWebAppName = "financehub-web-$suffix"
$keyVaultName = "fh-kv-$suffix"
$appInsightsName = "financehub-ai-$suffix"
$entraAppName = "FinanceHub-SqlDb-$suffix"

Write-Log "Generated resource names with suffix: $suffix" "INFO"
Write-Log "  SQL Server: $sqlServerName" "INFO"
Write-Log "  Function App: $functionAppName" "INFO"
Write-Log "  Static Web App: $staticWebAppName" "INFO"
Write-Log "  Key Vault: $keyVaultName" "INFO"

# Register required providers
Write-Log "Registering required Azure providers..." "INFO"
Register-AzureProvider -Namespace "Microsoft.Sql"
Register-AzureProvider -Namespace "Microsoft.Insights"
Register-AzureProvider -Namespace "Microsoft.Web"
Register-AzureProvider -Namespace "Microsoft.Storage"
Register-AzureProvider -Namespace "Microsoft.KeyVault"

# Create Resource Group
Write-Log "Creating resource group: $ResourceGroupName" "INFO"
$rg = az group create --name $ResourceGroupName --location $Location | ConvertFrom-Json
Write-Log "Resource group created: $($rg.id)" "INFO"

# Create Storage Account (Main)
Write-Log "Creating main storage account: $storageAccountName" "INFO"
$storage = az storage account create `
    --name $storageAccountName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2 `
    --min-tls-version TLS1_2 | ConvertFrom-Json
Write-Log "Main storage account created" "INFO"

# Create blob containers
Write-Log "Creating blob containers..." "INFO"
$storageKey = az storage account keys list --resource-group $ResourceGroupName --account-name $storageAccountName --query "[0].value" -o tsv
az storage container create --name "invoices" --account-name $storageAccountName --account-key $storageKey | Out-Null
az storage container create --name "receipts" --account-name $storageAccountName --account-key $storageKey | Out-Null
az storage container create --name "certificates" --account-name $storageAccountName --account-key $storageKey | Out-Null
Write-Log "Blob containers created" "INFO"

# Create Function Storage Account
Write-Log "Creating function storage account: $functionStorageName" "INFO"
$funcStorage = az storage account create `
    --name $functionStorageName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2 | ConvertFrom-Json
Write-Log "Function storage account created" "INFO"

# Create Application Insights
Write-Log "Creating Application Insights: $appInsightsName" "INFO"
$appInsights = az monitor app-insights component create `
    --app $appInsightsName `
    --location $Location `
    --resource-group $ResourceGroupName `
    --application-type web | ConvertFrom-Json
Write-Log "Application Insights created" "INFO"

# Create Key Vault
Write-Log "Creating Key Vault: $keyVaultName" "INFO"
$keyVault = az keyvault create `
    --name $keyVaultName `
    --resource-group $ResourceGroupName `
    --location $Location | ConvertFrom-Json
Write-Log "Key Vault created: $($keyVault.properties.vaultUri)" "INFO"

# Create SQL Server
Write-Log "Creating SQL Server: $sqlServerName" "INFO"
$sqlServer = az sql server create `
    --name $sqlServerName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --admin-user $sqlAdminUser `
    --admin-password $sqlAdminPassword `
    --enable-public-network true | ConvertFrom-Json
Write-Log "SQL Server created: $($sqlServer.fullyQualifiedDomainName)" "INFO"

# Configure SQL firewall
Write-Log "Configuring SQL Server firewall rules..." "INFO"
az sql server firewall-rule create `
    --resource-group $ResourceGroupName `
    --server $sqlServerName `
    --name "AllowAzureServices" `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 | Out-Null

$myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content.Trim()
az sql server firewall-rule create `
    --resource-group $ResourceGroupName `
    --server $sqlServerName `
    --name "ClientIP" `
    --start-ip-address $myIp `
    --end-ip-address $myIp | Out-Null
Write-Log "Added firewall rule for your IP: $myIp" "INFO"

# Create SQL Database
Write-Log "Creating SQL Database: $sqlDbName (Serverless)" "INFO"
$sqlDb = az sql db create `
    --resource-group $ResourceGroupName `
    --server $sqlServerName `
    --name $sqlDbName `
    --edition GeneralPurpose `
    --family Gen5 `
    --capacity 1 `
    --compute-model Serverless `
    --auto-pause-delay 60 | ConvertFrom-Json
Write-Log "SQL Database created (Auto-pause: 60 minutes)" "INFO"

# Store secrets in Key Vault
Write-Log "Storing secrets in Key Vault..." "INFO"
$storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=$storageAccountName;AccountKey=$storageKey;EndpointSuffix=core.windows.net"
$sqlConnectionString = "Server=tcp:$($sqlServer.fullyQualifiedDomainName),1433;Initial Catalog=$sqlDbName;Persist Security Info=False;User ID=$sqlAdminUser;Password=$sqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az keyvault secret set --vault-name $keyVaultName --name "SqlConnectionString" --value $sqlConnectionString | Out-Null
az keyvault secret set --vault-name $keyVaultName --name "StorageConnectionString" --value $storageConnectionString | Out-Null
az keyvault secret set --vault-name $keyVaultName --name "SqlAdminPassword" --value $sqlAdminPassword | Out-Null
Write-Log "Secrets stored in Key Vault" "INFO"

# Create Function App
Write-Log "Creating Function App: $functionAppName" "INFO"
$functionApp = az functionapp create `
    --name $functionAppName `
    --storage-account $functionStorageName `
    --resource-group $ResourceGroupName `
    --consumption-plan-location $Location `
    --runtime dotnet-isolated `
    --runtime-version 8 `
    --functions-version 4 `
    --app-insights $appInsightsName `
    --assign-identity | ConvertFrom-Json
Write-Log "Function App created: https://$($functionApp.defaultHostName)" "INFO"

# Get Managed Identity
$principalId = $functionApp.identity.principalId
Write-Log "Function App Managed Identity: $principalId" "INFO"

# Grant Key Vault access
Write-Log "Granting Function App access to Key Vault..." "INFO"
az keyvault set-policy `
    --name $keyVaultName `
    --object-id $principalId `
    --secret-permissions get list | Out-Null
Write-Log "Key Vault access granted" "INFO"

# Configure Function App settings
Write-Log "Configuring Function App settings..." "INFO"
az functionapp config appsettings set `
    --name $functionAppName `
    --resource-group $ResourceGroupName `
    --settings `
        "KeyVaultUri=$($keyVault.properties.vaultUri)" `
        "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" `
        "BaseCurrency=GBP" | Out-Null
Write-Log "Function App settings configured" "INFO"

# Create Entra App Registration
Write-Log "Creating NEW Entra App Registration: $entraAppName" "INFO"
$entraApp = az ad app create --display-name $entraAppName | ConvertFrom-Json
$appId = $entraApp.appId
Write-Log "Entra App created: $appId" "INFO"

# Create Service Principal
Write-Log "Creating Service Principal..." "INFO"
az ad sp create --id $appId | Out-Null
Write-Log "Service Principal created" "INFO"

# Create Static Web App
Write-Log "Creating Static Web App: $staticWebAppName" "INFO"
$swaLocation = if ($Location -eq "uksouth") { "westeurope" } else { $Location }
$staticWebApp = az staticwebapp create `
    --name $staticWebAppName `
    --resource-group $ResourceGroupName `
    --location $swaLocation `
    --sku Free | ConvertFrom-Json
$swaUrl = "https://$($staticWebApp.defaultHostname)"
Write-Log "Static Web App created: $swaUrl" "INFO"

# Get SWA deployment token
$swaToken = az staticwebapp secrets list --name $staticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv
Write-Log "Static Web App deployment token retrieved" "INFO"

# Update Entra app redirect URIs
Write-Log "Configuring Entra App redirect URIs..." "INFO"
$redirectUris = @(
    "$swaUrl/.auth/login/aad/callback"
    $swaUrl
)
if ($CustomDomain) {
    $redirectUris += "https://$CustomDomain/.auth/login/aad/callback"
    $redirectUris += "https://$CustomDomain"
}
az ad app update --id $appId --web-redirect-uris $redirectUris | Out-Null
Write-Log "Redirect URIs configured" "INFO"

# Configure custom domain if provided
if ($CustomDomain) {
    Write-Log "Configuring custom domain: $CustomDomain" "INFO"
    
    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host "CUSTOM DOMAIN CONFIGURATION" -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Step 1: Add the following DNS record at your domain registrar:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Record Type : CNAME" -ForegroundColor White
    Write-Host "  Name/Host   : $($CustomDomain.Split('.')[0])" -ForegroundColor Green
    Write-Host "  Value/Target: $($staticWebApp.defaultHostname)" -ForegroundColor Green
    Write-Host "  TTL         : 3600 (or Auto)" -ForegroundColor White
    Write-Host ""
    
    # Try to add the custom domain (will provide additional info if validation is needed)
    Write-Host "Step 2: Attempting to configure custom domain in Azure..." -ForegroundColor Yellow
    try {
        $hostnameResult = az staticwebapp hostname set `
            --name $staticWebAppName `
            --resource-group $ResourceGroupName `
            --hostname $CustomDomain `
            2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Custom domain configured successfully!" "SUCCESS"
            Write-Host "✓ Custom domain configured: https://$CustomDomain" -ForegroundColor Green
        } else {
            Write-Log "Custom domain validation pending (DNS not yet propagated)" "WARNING"
            Write-Host ""
            Write-Host "DNS record needs to propagate (5-30 minutes)." -ForegroundColor Yellow
            Write-Host ""
            Write-Host "After DNS propagates, run this command to complete setup:" -ForegroundColor Yellow
            Write-Host "  az staticwebapp hostname set --name $staticWebAppName --resource-group $ResourceGroupName --hostname $CustomDomain" -ForegroundColor Cyan
        }
    } catch {
        Write-Log "Custom domain configuration will need DNS setup first" "INFO"
        Write-Host ""
        Write-Host "After adding the DNS record and waiting for propagation, run:" -ForegroundColor Yellow
        Write-Host "  az staticwebapp hostname set --name $staticWebAppName --resource-group $ResourceGroupName --hostname $CustomDomain" -ForegroundColor Cyan
    }
    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host ""
}

# Save deployment configuration
$deploymentConfig = @{
    Timestamp = $timestamp
    SubscriptionId = $SubscriptionId
    TenantId = $tenantId
    TenantDomain = $tenantDomain
    ResourceGroup = $ResourceGroupName
    Location = $Location
    Suffix = $suffix
    Resources = @{
        SqlServer = $sqlServerName
        SqlServerFQDN = $sqlServer.fullyQualifiedDomainName
        SqlDatabase = $sqlDbName
        SqlAdminUser = $sqlAdminUser
        StorageAccount = $storageAccountName
        FunctionApp = $functionAppName
        FunctionAppUrl = "https://$($functionApp.defaultHostName)"
        StaticWebApp = $staticWebAppName
        StaticWebAppUrl = $swaUrl
        KeyVault = $keyVaultName
        KeyVaultUrl = $keyVault.properties.vaultUri
        ApplicationInsights = $appInsightsName
        EntraAppId = $appId
        FunctionAppIdentity = $principalId
    }
    DeploymentToken = $swaToken
    CustomDomain = $CustomDomain
}

$configOutputFile = Join-Path $scriptRoot "deployment-config-$suffix.json"
$deploymentConfig | ConvertTo-Json -Depth 10 | Set-Content $configOutputFile
Write-Log "Deployment configuration saved to: $configOutputFile" "INFO"

# Create SQL setup script for Managed Identity
$sqlSetupScript = @"
-- Connect to the financehub database and run this script
-- This grants the Function App's Managed Identity access to the database

CREATE USER [$functionAppName] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [$functionAppName];
ALTER ROLE db_datawriter ADD MEMBER [$functionAppName];
ALTER ROLE db_ddladmin ADD MEMBER [$functionAppName];
GO
"@

$sqlSetupFile = Join-Path $scriptRoot "sql-setup-managed-identity-$suffix.sql"
$sqlSetupScript | Set-Content $sqlSetupFile
Write-Log "SQL setup script created: $sqlSetupFile" "INFO"

# Summary
Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host " DEPLOYMENT COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host ""
Write-Host "RESOURCE DETAILS:" -ForegroundColor Yellow
Write-Host "--------------------------------------------------" -ForegroundColor Yellow
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "Location: $Location" -ForegroundColor White
Write-Host ""
Write-Host "SQL SERVER:" -ForegroundColor Yellow
Write-Host "  Server: $($sqlServer.fullyQualifiedDomainName)" -ForegroundColor White
Write-Host "  Database: $sqlDbName" -ForegroundColor White
Write-Host "  Admin User: $sqlAdminUser" -ForegroundColor White
Write-Host "  Admin Password: (stored in Key Vault)" -ForegroundColor White
Write-Host ""
Write-Host "FUNCTION APP:" -ForegroundColor Yellow
Write-Host "  Name: $functionAppName" -ForegroundColor White
Write-Host "  URL: https://$($functionApp.defaultHostName)" -ForegroundColor White
Write-Host "  Managed Identity: $principalId" -ForegroundColor White
Write-Host ""
Write-Host "STATIC WEB APP:" -ForegroundColor Yellow
Write-Host "  Name: $staticWebAppName" -ForegroundColor White
Write-Host "  URL: $swaUrl" -ForegroundColor White
Write-Host "  Entra App ID: $appId" -ForegroundColor White
if ($CustomDomain) {
    Write-Host "  Custom Domain: $CustomDomain (configure DNS)" -ForegroundColor White
}
Write-Host ""
Write-Host "KEY VAULT:" -ForegroundColor Yellow
Write-Host "  Name: $keyVaultName" -ForegroundColor White
Write-Host "  URL: $($keyVault.properties.vaultUri)" -ForegroundColor White
Write-Host ""
Write-Host "STORAGE:" -ForegroundColor Yellow
Write-Host "  Main: $storageAccountName" -ForegroundColor White
Write-Host "  Containers: invoices, receipts, certificates" -ForegroundColor White
Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host " NEXT STEPS:" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "1. Update StaticWebApp API endpoint:" -ForegroundColor White
Write-Host "   Edit: StaticWebApp\src\services\apiService.js" -ForegroundColor Gray
Write-Host "   Set API_BASE = 'https://$($functionApp.defaultHostName)/api'" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Deploy Function App:" -ForegroundColor White
Write-Host "   cd FunctionApp" -ForegroundColor Gray
Write-Host "   func azure functionapp publish $functionAppName" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Deploy Static Web App:" -ForegroundColor White
Write-Host "   cd StaticWebApp" -ForegroundColor Gray
Write-Host "   npm install && npm run build" -ForegroundColor Gray
Write-Host "   `$env:DEPLOYMENT_TOKEN = '$swaToken'" -ForegroundColor Gray
Write-Host "   npx @azure/static-web-apps-cli deploy ./dist --deployment-token `$env:DEPLOYMENT_TOKEN --env production" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Initialize Database:" -ForegroundColor White
Write-Host "   cd FunctionApp" -ForegroundColor Gray
Write-Host "   dotnet ef database update" -ForegroundColor Gray
Write-Host ""
Write-Host "5. Grant SQL Managed Identity access:" -ForegroundColor White
Write-Host "   Run the script: $sqlSetupFile" -ForegroundColor Gray
Write-Host ""
Write-Host "Configuration saved to: $configOutputFile" -ForegroundColor Cyan
Write-Host "Full log: $logFile" -ForegroundColor Cyan
Write-Host ""
Write-Log "Deployment complete!" "INFO"
