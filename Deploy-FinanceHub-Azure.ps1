<#
.SYNOPSIS
    End-to-End Finance Hub Deployment to Azure
    
.DESCRIPTION
    Deploys a complete Finance Hub solution with:
    - Azure SQL Database (Serverless)
    - Azure Function App with Managed Identity
    - Static Web App with Entra SSO
    - Key Vault for secrets
    - Application Insights
    - Storage Account for documents
    
    Fully automated, idempotent, and multi-tenant ready.

.PARAMETER SubscriptionId
    Target Azure subscription ID (optional - will prompt if not provided)
    
.PARAMETER Location
    Azure region (default: uksouth)
    
.PARAMETER Environment
    Environment name: dev, test, prod (default: prod)
    
.PARAMETER TenantDomain
    Your Entra tenant domain (e.g., kempy.onmicrosoft.com)

.EXAMPLE
    .\Deploy-FinanceHub-Azure.ps1 -Environment prod
    
.EXAMPLE
    .\Deploy-FinanceHub-Azure.ps1 -SubscriptionId "xxx-xxx" -Location "uksouth" -Environment dev
#>

param(
    [string]$SubscriptionId,
    [string]$Location = "uksouth",
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment = "prod",
    [string]$TenantDomain,
    [string]$ResourcePrefix = "financehub"
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# ===========================
# Configuration
# ===========================
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logFile = Join-Path $scriptRoot "deployment-$timestamp.log"
$configFile = Join-Path $scriptRoot "financehub-deployment-config.json"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $logMessage = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $logFile -Value $logMessage
}

function Test-AzureCLI {
    try {
        $null = az version
        return $true
    } catch {
        return $false
    }
}

function Get-SavedConfig {
    if (Test-Path $configFile) {
        try {
            return Get-Content $configFile | ConvertFrom-Json
        } catch {
            return $null
        }
    }
    return $null
}

function Save-ConfigValue {
    param([string]$Key, [string]$Value)
    
    $config = Get-SavedConfig
    if (-not $config) {
        $config = @{}
    }
    
    # Convert to hashtable if it's a PSCustomObject
    if ($config -is [PSCustomObject]) {
        $configHash = @{}
        $config.PSObject.Properties | ForEach-Object { $configHash[$_.Name] = $_.Value }
        $config = $configHash
    }
    
    $config[$Key] = $Value
    $config | ConvertTo-Json -Depth 10 | Set-Content $configFile
}

function Get-ConfigValue {
    param(
        [string]$Key,
        [string]$Prompt,
        [string]$Default = "",
        [switch]$Required
    )
    
    $savedConfig = Get-SavedConfig
    $savedValue = $null
    
    if ($savedConfig -and $savedConfig.PSObject.Properties[$Key]) {
        $savedValue = $savedConfig.$Key
    }
    
    if ($savedValue) {
        $displayValue = if ($savedValue.Length -gt 50) { $savedValue.Substring(0, 47) + "..." } else { $savedValue }
        $input = Read-Host "$Prompt [$displayValue]"
        if ([string]::IsNullOrWhiteSpace($input)) {
            return $savedValue
        }
        $value = $input
    } elseif ($Default) {
        $input = Read-Host "$Prompt [$Default]"
        $value = if ([string]::IsNullOrWhiteSpace($input)) { $Default } else { $input }
    } else {
        $value = Read-Host $Prompt
        if ($Required) {
            while ([string]::IsNullOrWhiteSpace($value)) {
                Write-Host "This value is required" -ForegroundColor Yellow
                $value = Read-Host $Prompt
            }
        }
    }
    
    Save-ConfigValue -Key $Key -Value $value
    return $value
}

# ===========================
# Prerequisites Check
# ===========================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Finance Hub - Azure Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Log "Starting Finance Hub deployment to Azure" "INFO"

if (-not (Test-AzureCLI)) {
    Write-Log "Azure CLI is not installed. Please install from: https://aka.ms/azure-cli" "ERROR"
    throw "Azure CLI is required"
}

# Check if logged in
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Log "Not logged into Azure. Running az login..." "WARN"
    Write-Host "Please login to Azure..." -ForegroundColor Yellow
    az login --use-device-code
    $account = az account show | ConvertFrom-Json
}

Write-Host ""
Write-Host "Current Azure Context:" -ForegroundColor Green
Write-Host "  Account: $($account.user.name)" -ForegroundColor White
Write-Host "  Tenant: $($account.tenantId)" -ForegroundColor White
Write-Host ""

# ===========================
# Interactive Configuration
# ===========================
Write-Host "Configuration (previous values shown in [brackets]):" -ForegroundColor Cyan
Write-Host ""

# Subscription
if (-not $SubscriptionId) {
    $savedConfig = Get-SavedConfig
    if ($savedConfig -and $savedConfig.SubscriptionId) {
        $useCurrentSub = Read-Host "Use current subscription '$($account.name)'? (Y/N) [Y]"
        if ($useCurrentSub -eq 'N' -or $useCurrentSub -eq 'n') {
            $SubscriptionId = Get-ConfigValue -Key "SubscriptionId" -Prompt "Enter Subscription ID" -Required
        } else {
            $SubscriptionId = $account.id
            Save-ConfigValue -Key "SubscriptionId" -Value $SubscriptionId
        }
    } else {
        $SubscriptionId = $account.id
        Save-ConfigValue -Key "SubscriptionId" -Value $SubscriptionId
    }
}

Write-Log "Setting subscription to: $SubscriptionId" "INFO"
az account set --subscription $SubscriptionId

# Refresh account info after subscription change
$account = az account show | ConvertFrom-Json
$tenantId = $account.tenantId

# Tenant Domain
if (-not $TenantDomain) {
    $defaultDomain = $account.user.name.Split('@')[1]
    $TenantDomain = Get-ConfigValue -Key "TenantDomain" -Prompt "Enter Tenant Domain (e.g., kempy.onmicrosoft.com)" -Default $defaultDomain
}

# Location
if (-not $Location -or $Location -eq "uksouth") {
    $Location = Get-ConfigValue -Key "Location" -Prompt "Enter Azure Region" -Default "uksouth"
}

# Resource Prefix
if (-not $ResourcePrefix -or $ResourcePrefix -eq "financehub") {
    $ResourcePrefix = Get-ConfigValue -Key "ResourcePrefix" -Prompt "Enter Resource Prefix (e.g., financehub or clientname-fh)" -Default "financehub"
}

# Environment
if (-not $Environment -or $Environment -eq "prod") {
    $envInput = Get-ConfigValue -Key "Environment" -Prompt "Enter Environment (dev/test/prod)" -Default "prod"
    $Environment = $envInput
}

Write-Host ""
Write-Log "Configuration Summary:" "INFO"
Write-Log "  Subscription: $SubscriptionId" "INFO"
Write-Log "  Tenant ID: $tenantId" "INFO"
Write-Log "  Tenant Domain: $TenantDomain" "INFO"
Write-Log "  Location: $Location" "INFO"
Write-Log "  Resource Prefix: $ResourcePrefix" "INFO"
Write-Log "  Environment: $Environment" "INFO"
Write-Host ""

$confirm = Read-Host "Proceed with deployment? (Y/N) [Y]"
if ($confirm -eq 'N' -or $confirm -eq 'n') {
    Write-Host "Deployment cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Starting deployment..." -ForegroundColor Green
Write-Host ""

# ===========================
# Resource Naming
# ===========================
$rgName = "rg-$ResourcePrefix-$Environment"
$sqlServerName = "$ResourcePrefix-sql-$Environment-$(Get-Random -Maximum 9999)"
$sqlDbName = "financehub"
$funcAppName = "$ResourcePrefix-func-$Environment-$(Get-Random -Maximum 9999)"
$storageAccountName = "$($ResourcePrefix)stor$Environment$(Get-Random -Maximum 9999)".Replace("-","").ToLower()
$swaName = "$ResourcePrefix-web-$Environment"
$kvName = "$ResourcePrefix-kv-$Environment-$(Get-Random -Maximum 999)"
$appInsightsName = "$ResourcePrefix-ai-$Environment"
$sqlAdminUser = "sqladmin"
$sqlAdminPassword = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 16 | ForEach-Object {[char]$_}) + "Aa1!"

Write-Log "Resource Group: $rgName" "INFO"
Write-Log "SQL Server: $sqlServerName" "INFO"
Write-Log "Function App: $funcAppName" "INFO"
Write-Log "Static Web App: $swaName" "INFO"

# ===========================
# Create Resource Group
# ===========================
Write-Log "Creating resource group: $rgName" "INFO"
$rg = az group create --name $rgName --location $Location | ConvertFrom-Json
Write-Log "Resource group created: $($rg.id)" "INFO"

# ===========================
# Create Storage Account
# ===========================
Write-Log "Creating storage account: $storageAccountName" "INFO"
$storage = az storage account create `
    --name $storageAccountName `
    --resource-group $rgName `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2 `
    --min-tls-version TLS1_2 `
    --allow-blob-public-access false | ConvertFrom-Json

$storageKey = (az storage account keys list --account-name $storageAccountName --resource-group $rgName | ConvertFrom-Json)[0].value
$storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=$storageAccountName;AccountKey=$storageKey;EndpointSuffix=core.windows.net"

# Create blob containers
Write-Log "Creating blob containers..." "INFO"
az storage container create --name invoices --account-name $storageAccountName --account-key $storageKey --auth-mode key
az storage container create --name receipts --account-name $storageAccountName --account-key $storageKey --auth-mode key
az storage container create --name certificates --account-name $storageAccountName --account-key $storageKey --auth-mode key

# ===========================
# Create Application Insights
# ===========================
Write-Log "Creating Application Insights: $appInsightsName" "INFO"
$appInsights = az monitor app-insights component create `
    --app $appInsightsName `
    --location $Location `
    --resource-group $rgName `
    --application-type web | ConvertFrom-Json

$instrumentationKey = $appInsights.instrumentationKey
$appInsightsConnectionString = $appInsights.connectionString

# ===========================
# Create Key Vault
# ===========================
Write-Log "Creating Key Vault: $kvName" "INFO"
$kv = az keyvault create `
    --name $kvName `
    --resource-group $rgName `
    --location $Location `
    --enable-rbac-authorization false `
    --enabled-for-deployment true | ConvertFrom-Json

Write-Log "Key Vault created: $($kv.properties.vaultUri)" "INFO"

# ===========================
# Create Azure SQL Database
# ===========================
Write-Log "Creating SQL Server: $sqlServerName" "INFO"
$sqlServer = az sql server create `
    --name $sqlServerName `
    --resource-group $rgName `
    --location $Location `
    --admin-user $sqlAdminUser `
    --admin-password $sqlAdminPassword `
    --enable-public-network true | ConvertFrom-Json

Write-Log "Configuring SQL Server firewall..." "INFO"
az sql server firewall-rule create `
    --resource-group $rgName `
    --server $sqlServerName `
    --name AllowAzureServices `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0

# Get current IP and whitelist it
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=json").ip
Write-Log "Adding your IP to firewall: $myIp" "INFO"
az sql server firewall-rule create `
    --resource-group $rgName `
    --server $sqlServerName `
    --name AllowDeploymentIP `
    --start-ip-address $myIp `
    --end-ip-address $myIp

Write-Log "Creating SQL Database: $sqlDbName (Serverless)" "INFO"
$sqlDb = az sql db create `
    --resource-group $rgName `
    --server $sqlServerName `
    --name $sqlDbName `
    --edition GeneralPurpose `
    --compute-model Serverless `
    --family Gen5 `
    --capacity 1 `
    --min-capacity 0.5 `
    --auto-pause-delay 60 `
    --backup-storage-redundancy Local | ConvertFrom-Json

$sqlConnectionString = "Server=tcp:$sqlServerName.database.windows.net,1433;Initial Catalog=$sqlDbName;Persist Security Info=False;User ID=$sqlAdminUser;Password=$sqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Log "SQL Database created. Auto-pause delay: 60 minutes" "INFO"

# ===========================
# Store Secrets in Key Vault
# ===========================
Write-Log "Storing secrets in Key Vault..." "INFO"
az keyvault secret set --vault-name $kvName --name "SqlConnectionString" --value $sqlConnectionString
az keyvault secret set --vault-name $kvName --name "StorageConnectionString" --value $storageConnectionString
az keyvault secret set --vault-name $kvName --name "SqlAdminPassword" --value $sqlAdminPassword

# ===========================
# Create Function App
# ===========================
Write-Log "Creating Function App: $funcAppName" "INFO"

# Create a dedicated storage account for function app
$funcStorageName = "$($ResourcePrefix)func$Environment$(Get-Random -Maximum 9999)".Replace("-","").ToLower()
az storage account create `
    --name $funcStorageName `
    --resource-group $rgName `
    --location $Location `
    --sku Standard_LRS

$funcApp = az functionapp create `
    --name $funcAppName `
    --resource-group $rgName `
    --storage-account $funcStorageName `
    --consumption-plan-location $Location `
    --runtime dotnet-isolated `
    --runtime-version 8 `
    --functions-version 4 `
    --os-type Windows `
    --app-insights $appInsightsName `
    --disable-app-insights false | ConvertFrom-Json

Write-Log "Enabling Managed Identity for Function App..." "INFO"
$funcIdentity = az functionapp identity assign --name $funcAppName --resource-group $rgName | ConvertFrom-Json
$funcPrincipalId = $funcIdentity.principalId

Write-Log "Function App Managed Identity Principal ID: $funcPrincipalId" "INFO"

# Grant Key Vault access to Function App
Write-Log "Granting Function App access to Key Vault..." "INFO"
az keyvault set-policy `
    --name $kvName `
    --object-id $funcPrincipalId `
    --secret-permissions get list

# Configure Function App settings
Write-Log "Configuring Function App settings..." "INFO"
az functionapp config appsettings set `
    --name $funcAppName `
    --resource-group $rgName `
    --settings `
        "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString" `
        "AzureWebJobsStorage__accountName=$funcStorageName" `
        "KeyVaultUrl=$($kv.properties.vaultUri)" `
        "WEBSITE_RUN_FROM_PACKAGE=1"

# Enable CORS for Function App
Write-Log "Configuring CORS for Function App..." "INFO"
az functionapp cors add --name $funcAppName --resource-group $rgName --allowed-origins "*"

# ===========================
# Create Entra App Registration
# ===========================
Write-Log "Creating Entra App Registration for SSO..." "INFO"

$appName = "FinanceHub-$Environment"
$redirectUri = "https://$swaName.azurestaticapps.net/.auth/login/aad/callback"

# Check if app already exists
$existingApp = az ad app list --display-name $appName | ConvertFrom-Json
if ($existingApp -and $existingApp.Count -gt 0) {
    Write-Log "App registration already exists, using existing app..." "WARN"
    $app = $existingApp[0]
    $appId = $app.appId
} else {
    $app = az ad app create `
        --display-name $appName `
        --sign-in-audience AzureADMyOrg `
        --web-redirect-uris $redirectUri `
        --enable-id-token-issuance true `
        --enable-access-token-issuance true | ConvertFrom-Json
    
    $appId = $app.appId
    Write-Log "App Registration created: $appId" "INFO"
}

# Create Service Principal
$sp = az ad sp list --filter "appId eq '$appId'" | ConvertFrom-Json
if (-not $sp -or $sp.Count -eq 0) {
    Write-Log "Creating Service Principal..." "INFO"
    $sp = az ad sp create --id $appId | ConvertFrom-Json
}

# ===========================
# Deploy Static Web App
# ===========================
Write-Log "Creating Static Web App: $swaName" "INFO"
Write-Log "Note: SWA requires GitHub/Azure DevOps for CI/CD. Manual deployment token will be provided." "WARN"

$swa = az staticwebapp create `
    --name $swaName `
    --resource-group $rgName `
    --location $Location `
    --sku Free | ConvertFrom-Json

$swaHostname = $swa.defaultHostname
Write-Log "Static Web App created: https://$swaHostname" "INFO"

# Get deployment token
$swaToken = az staticwebapp secrets list --name $swaName --resource-group $rgName --query "properties.apiKey" -o tsv

Write-Log "Static Web App deployment token retrieved" "INFO"

# ===========================
# Configure Entra Authentication
# ===========================
Write-Log "Configuring Entra authentication for Static Web App..." "INFO"

$authConfig = @{
    platform = @{
        enabled = $true
    }
    globalValidation = @{
        requireAuthentication = $true
        unauthenticatedClientAction = "RedirectToLoginPage"
    }
    identityProviders = @{
        azureActiveDirectory = @{
            enabled = $true
            registration = @{
                openIdIssuer = "https://login.microsoftonline.com/$tenantId/v2.0"
                clientId = $appId
            }
        }
    }
} | ConvertTo-Json -Depth 10

$authConfigPath = Join-Path $scriptRoot "StaticWebApp\staticwebapp.config.json"
if (Test-Path $authConfigPath) {
    $existingConfig = Get-Content $authConfigPath | ConvertFrom-Json
    $existingConfig | Add-Member -NotePropertyName "auth" -NotePropertyValue ($authConfig | ConvertFrom-Json) -Force
    $existingConfig | ConvertTo-Json -Depth 10 | Set-Content $authConfigPath
    Write-Log "Updated existing staticwebapp.config.json with auth settings" "INFO"
}

# ===========================
# Initialize Database Schema
# ===========================
Write-Log "Database will be initialized on first Function App deployment with EF Core migrations" "INFO"

# ===========================
# Output Configuration
# ===========================
Write-Log "==================================================" "INFO"
Write-Log "DEPLOYMENT COMPLETED SUCCESSFULLY!" "INFO"
Write-Log "==================================================" "INFO"
Write-Log "" "INFO"
Write-Log "RESOURCE DETAILS:" "INFO"
Write-Log "--------------------------------------------------" "INFO"
Write-Log "Resource Group: $rgName" "INFO"
Write-Log "Location: $Location" "INFO"
Write-Log "" "INFO"
Write-Log "SQL SERVER:" "INFO"
Write-Log "  Server: $sqlServerName.database.windows.net" "INFO"
Write-Log "  Database: $sqlDbName" "INFO"
Write-Log "  Admin User: $sqlAdminUser" "INFO"
Write-Log "  Connection String stored in Key Vault" "INFO"
Write-Log "" "INFO"
Write-Log "FUNCTION APP:" "INFO"
Write-Log "  Name: $funcAppName" "INFO"
Write-Log "  URL: https://$funcAppName.azurewebsites.net" "INFO"
Write-Log "  Managed Identity: Enabled" "INFO"
Write-Log "" "INFO"
Write-Log "STATIC WEB APP:" "INFO"
Write-Log "  Name: $swaName" "INFO"
Write-Log "  URL: https://$swaHostname" "INFO"
Write-Log "  Entra App ID: $appId" "INFO"
Write-Log "" "INFO"
Write-Log "KEY VAULT:" "INFO"
Write-Log "  Name: $kvName" "INFO"
Write-Log "  URL: $($kv.properties.vaultUri)" "INFO"
Write-Log "" "INFO"
Write-Log "STORAGE ACCOUNT:" "INFO"
Write-Log "  Name: $storageAccountName" "INFO"
Write-Log "  Containers: invoices, receipts, certificates" "INFO"
Write-Log "" "INFO"
Write-Log "APPLICATION INSIGHTS:" "INFO"
Write-Log "  Name: $appInsightsName" "INFO"
Write-Log "" "INFO"
Write-Log "==================================================" "INFO"
Write-Log "NEXT STEPS:" "INFO"
Write-Log "==================================================" "INFO"
Write-Log "1. Deploy Function App:" "INFO"
Write-Log "   cd FunctionApp" "INFO"
Write-Log "   func azure functionapp publish $funcAppName" "INFO"
Write-Log "" "INFO"
Write-Log "2. Deploy Static Web App:" "INFO"
Write-Log "   cd StaticWebApp" "INFO"
Write-Log "   npx @azure/static-web-apps-cli deploy --deployment-token `"$swaToken`"" "INFO"
Write-Log "" "INFO"
Write-Log "3. Update app registration redirect URI to: https://$swaHostname/.auth/login/aad/callback" "INFO"
Write-Log "" "INFO"
Write-Log "4. Grant SQL access to Function App Managed Identity:" "INFO"
Write-Log "   Run the SQL script in sql-setup-managed-identity.sql" "INFO"
Write-Log "" "INFO"
Write-Log "Configuration saved to: $logFile" "INFO"

# Save deployment config
$deployConfig = @{
    SubscriptionId = $SubscriptionId
    TenantId = $tenantId
    ResourceGroup = $rgName
    Location = $Location
    Environment = $Environment
    SqlServer = "$sqlServerName.database.windows.net"
    SqlDatabase = $sqlDbName
    FunctionAppName = $funcAppName
    FunctionAppUrl = "https://$funcAppName.azurewebsites.net"
    StaticWebAppName = $swaName
    StaticWebAppUrl = "https://$swaHostname"
    StaticWebAppToken = $swaToken
    KeyVaultName = $kvName
    KeyVaultUrl = $kv.properties.vaultUri
    StorageAccountName = $storageAccountName
    AppInsightsName = $appInsightsName
    EntraAppId = $appId
    FunctionAppPrincipalId = $funcPrincipalId
    DeploymentDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

$configPath = Join-Path $scriptRoot "deployment-config-$Environment.json"
$deployConfig | ConvertTo-Json -Depth 10 | Set-Content $configPath
Write-Log "Deployment configuration saved to: $configPath" "INFO"

# Create SQL setup script
$sqlSetupScript = @"
-- Run this script as SQL admin to grant Function App access via Managed Identity
-- Connect to database: $sqlDbName

-- Create user for Function App Managed Identity
CREATE USER [$funcAppName] FROM EXTERNAL PROVIDER;

-- Grant necessary permissions
ALTER ROLE db_datareader ADD MEMBER [$funcAppName];
ALTER ROLE db_datawriter ADD MEMBER [$funcAppName];
ALTER ROLE db_ddladmin ADD MEMBER [$funcAppName];

PRINT 'Managed Identity access granted successfully';
"@

$sqlSetupPath = Join-Path $scriptRoot "sql-setup-managed-identity.sql"
$sqlSetupScript | Set-Content $sqlSetupPath
Write-Log "SQL setup script created: $sqlSetupPath" "INFO"

Write-Log "Deployment complete! Check $logFile for full details." "INFO"
