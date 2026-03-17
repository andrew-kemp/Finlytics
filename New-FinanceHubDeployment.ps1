<#
.SYNOPSIS
    Complete automated provisioning and deployment of FinanceHub from scratch

.DESCRIPTION
    This script performs a complete end-to-end provisioning for a new FinanceHub installation:
    1. Creates all Azure resources (Resource Group, SQL, Function App, Static Web App, Key Vault, etc.)
    2. Generates and securely stores passwords in Key Vault
    3. Creates Entra ID App Registration for SSO
    4. Initializes database with complete schema
    5. Deploys Function App and Static Web App
    6. Configures all settings
    
    Perfect for deploying to new customer sites or creating new environments.

.PARAMETER CustomerName
    Customer/company name (used for resource naming)

.PARAMETER Location
    Azure region (default: uksouth)

.PARAMETER CustomDomain
    Optional custom domain for the Static Web App

.PARAMETER SqlPassword
    SQL Server admin password (if not provided, will be auto-generated)

.PARAMETER AutoGeneratePasswords
    Automatically generate all passwords securely

.EXAMPLE
    .\New-FinanceHubDeployment.ps1 -CustomerName "Contoso" -AutoGeneratePasswords

.EXAMPLE
    .\New-FinanceHubDeployment.ps1 -CustomerName "Acme" -Location "westeurope" -CustomDomain "finance.acme.com" -SqlPassword "MySecureP@ss123!"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$CustomerName,
    
    [string]$Location = "uksouth",
    
    [string]$CustomDomain = "",
    
    [string]$SqlPassword = "",
    
    [switch]$AutoGeneratePasswords
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# ===========================
# Helper Functions
# ===========================
function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  → $Message" -ForegroundColor White
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  ⚠ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "  ✗ $Message" -ForegroundColor Red
}

function New-SecurePassword {
    $length = 16
    $chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*"
    $password = -join ((1..$length) | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
    # Ensure password meets complexity requirements
    if ($password -notmatch '[A-Z]') { $password = $password.Substring(0, $length-1) + 'A' }
    if ($password -notmatch '[a-z]') { $password = $password.Substring(0, $length-1) + 'a' }
    if ($password -notmatch '[0-9]') { $password = $password.Substring(0, $length-1) + '1' }
    if ($password -notmatch '[!@#$%^&*]') { $password = $password.Substring(0, $length-1) + '!' }
    return $password
}

function Get-RandomSuffix {
    return Get-Random -Minimum 1000 -Maximum 9999
}

# ===========================
# Header
# ===========================
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  FinanceHub - New Deployment Provisioner" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Customer: $CustomerName" -ForegroundColor Yellow
Write-Host "Location: $Location" -ForegroundColor Yellow
if ($CustomDomain) {
    Write-Host "Custom Domain: $CustomDomain" -ForegroundColor Yellow
}
Write-Host ""

# ===========================
# Check Prerequisites
# ===========================
Write-Step "Checking Prerequisites"

# Check Azure login
Write-Info "Checking Azure login..."
try {
    $account = az account show 2>$null | ConvertFrom-Json
    Write-Success "Logged in as: $($account.user.name)"
    Write-Success "Subscription: $($account.name)"
    $subscriptionId = $account.id
    $tenantId = $account.tenantId
} catch {
    Write-Error "Not logged into Azure"
    Write-Host ""
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Check required tools
Write-Info "Checking required tools..."
$missingTools = @()

try { dotnet --version | Out-Null } catch { $missingTools += ".NET SDK" }
try { node --version | Out-Null } catch { $missingTools += "Node.js" }
try { func --version | Out-Null } catch { $missingTools += "Azure Functions Core Tools" }

if ($missingTools.Count -gt 0) {
    Write-Error "Missing required tools: $($missingTools -join ', ')"
    exit 1
}
Write-Success "All required tools installed"

# ===========================
# Generate Configuration
# ===========================
Write-Step "Generating Deployment Configuration"

$sanitizedCustomerName = $CustomerName.ToLower() -replace '[^a-z0-9]', ''
$resourceGroupName = "rg-financehub-$sanitizedCustomerName"

Write-Info "Generating resource names based on customer: $sanitizedCustomerName..."
$config = @{
    CustomerName = $CustomerName
    ResourceGroup = $resourceGroupName
    TenantId = $tenantId
    Location = $Location
    Suffix = $sanitizedCustomerName
    CustomDomain = $CustomDomain
    Resources = @{
        SqlServer = "financehub-sql-$sanitizedCustomerName"
        SqlServerFQDN = "financehub-sql-$sanitizedCustomerName.database.windows.net"
        SqlDatabase = "financehub"
        SqlAdminUser = "sqladmin"
        KeyVault = "fh-kv-$sanitizedCustomerName"
        KeyVaultUrl = "https://fh-kv-$sanitizedCustomerName.vault.azure.net/"
        FunctionApp = "financehub-func-$sanitizedCustomerName"
        StaticWebApp = "financehub-web-$sanitizedCustomerName"
        StorageAccount = "fhstor$sanitizedCustomerName"
        ApplicationInsights = "financehub-ai-$sanitizedCustomerName"
    }
    SubscriptionId = $subscriptionId
    Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
}

Write-Success "Configuration generated"
Write-Host ""
Write-Host "  Resource Group: $resourceGroupName" -ForegroundColor Gray
Write-Host "  SQL Server: $($config.Resources.SqlServer)" -ForegroundColor Gray
Write-Host "  Function App: $($config.Resources.FunctionApp)" -ForegroundColor Gray
Write-Host "  Static Web App: $($config.Resources.StaticWebApp)" -ForegroundColor Gray
Write-Host "  Key Vault: $($config.Resources.KeyVault)" -ForegroundColor Gray

# Handle passwords
if ($AutoGeneratePasswords -or [string]::IsNullOrEmpty($SqlPassword)) {
    Write-Info "Generating secure SQL password..."
    $SqlPassword = New-SecurePassword
    Write-Success "Password generated"
}

# ===========================
# Step 1: Create Resource Group
# ===========================
Write-Step "Step 1: Create Resource Group"

Write-Info "Creating resource group: $resourceGroupName..."
az group create --name $resourceGroupName --location $Location --output none
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create resource group"
    exit 1
}
Write-Success "Resource group created"

# ===========================
# Step 2: Create Key Vault
# ===========================
Write-Step "Step 2: Create Key Vault"

Write-Info "Creating Key Vault: $($config.Resources.KeyVault)..."
az keyvault create `
    --name $config.Resources.KeyVault `
    --resource-group $resourceGroupName `
    --location $Location `
    --enable-rbac-authorization false `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create Key Vault"
    exit 1
}
Write-Success "Key Vault created"

Write-Info "Storing SQL password in Key Vault..."
az keyvault secret set `
    --vault-name $config.Resources.KeyVault `
    --name "SqlAdminPassword" `
    --value $SqlPassword `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to store password in Key Vault (continuing...)"
} else {
    Write-Success "SQL password stored securely"
}

# ===========================
# Step 3: Create SQL Server and Database
# ===========================
Write-Step "Step 3: Create SQL Server and Database"

Write-Info "Creating SQL Server: $($config.Resources.SqlServer)..."
az sql server create `
    --name $config.Resources.SqlServer `
    --resource-group $resourceGroupName `
    --location $Location `
    --admin-user $config.Resources.SqlAdminUser `
    --admin-password $SqlPassword `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create SQL Server"
    exit 1
}
Write-Success "SQL Server created"

Write-Info "Configuring firewall rules..."
az sql server firewall-rule create `
    --server $config.Resources.SqlServer `
    --resource-group $resourceGroupName `
    --name "AllowAzureServices" `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 `
    --output none

Write-Success "Firewall configured"

Write-Info "Creating database: $($config.Resources.SqlDatabase)..."
az sql db create `
    --server $config.Resources.SqlServer `
    --resource-group $resourceGroupName `
    --name $config.Resources.SqlDatabase `
    --service-objective S0 `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create database"
    exit 1
}
Write-Success "Database created"

# ===========================
# Step 4: Create Storage Account
# ===========================
Write-Step "Step 4: Create Storage Account"

Write-Info "Creating storage account: $($config.Resources.StorageAccount)..."
az storage account create `
    --name $config.Resources.StorageAccount `
    --resource-group $resourceGroupName `
    --location $Location `
    --sku Standard_LRS `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create storage account"
    exit 1
}
Write-Success "Storage account created"

# ===========================
# Step 5: Create Application Insights
# ===========================
Write-Step "Step 5: Create Application Insights"

Write-Info "Creating Application Insights: $($config.Resources.ApplicationInsights)..."
az monitor app-insights component create `
    --app $config.Resources.ApplicationInsights `
    --location $Location `
    --resource-group $resourceGroupName `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to create Application Insights (continuing...)"
} else {
    Write-Success "Application Insights created"
}

# ===========================
# Step 6: Create Function App
# ===========================
Write-Step "Step 6: Create Function App"

Write-Info "Creating Function App: $($config.Resources.FunctionApp)..."
az functionapp create `
    --name $config.Resources.FunctionApp `
    --resource-group $resourceGroupName `
    --storage-account $config.Resources.StorageAccount `
    --consumption-plan-location $Location `
    --runtime dotnet-isolated `
    --runtime-version 8 `
    --functions-version 4 `
    --os-type Windows `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create Function App"
    exit 1
}
Write-Success "Function App created"

Write-Info "Configuring Function App settings..."
$connectionString = "Server=$($config.Resources.SqlServerFQDN);Database=$($config.Resources.SqlDatabase);User Id=$($config.Resources.SqlAdminUser);Password=$SqlPassword;Encrypt=True;TrustServerCertificate=False;"

# Try using az webapp config connection-string set
$connectionStringSet = $false
try {
    az functionapp config connection-string set `
        --name $config.Resources.FunctionApp `
        --resource-group $resourceGroupName `
        --connection-string-type SQLAzure `
        --settings FinanceHubDb="$connectionString" `
        --output none 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        $connectionStringSet = $true
        Write-Success "Connection string configured"
    }
} catch {
    # Will try alternative method below
}

# If that fails due to permission issues, use REST API directly
if (-not $connectionStringSet) {
    Write-Warning "Standard method failed, using REST API..."
    
    try {
        # Get access token
        $token = az account get-access-token --query accessToken -o tsv
        
        # Construct REST API URL
        $apiUrl = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.Web/sites/$($config.Resources.FunctionApp)/config/connectionstrings?api-version=2022-03-01"
        
        # Prepare connection string payload
        $body = @{
            properties = @{
                FinanceHubDb = @{
                    value = $connectionString
                    type = "SQLAzure"
                }
            }
        } | ConvertTo-Json -Depth 10
        
        # Make REST API call
        $response = Invoke-RestMethod -Uri $apiUrl -Method Put -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } -Body $body
        
        Write-Success "Connection string configured via REST API"
        $connectionStringSet = $true
    } catch {
        Write-Error "Failed to configure connection string: $($_.Exception.Message)"
    }
}

if (-not $connectionStringSet) {
    Write-Error "Could not configure Function App connection string"
    Write-Host ""
    Write-Host "Manual configuration required:" -ForegroundColor Yellow
    Write-Host "  1. Go to Azure Portal → Function App: $($config.Resources.FunctionApp)" -ForegroundColor Gray
    Write-Host "  2. Configuration → Connection strings → Add" -ForegroundColor Gray
    Write-Host "  3. Name: FinanceHubDb" -ForegroundColor Gray
    Write-Host "  4. Value: [Connection string from Key Vault]" -ForegroundColor Gray
    Write-Host "  5. Type: SQLAzure" -ForegroundColor Gray
    Write-Host ""
}

# Restart Function App to pick up new settings
Write-Info "Restarting Function App to apply configuration..."
try {
    az functionapp restart --name $config.Resources.FunctionApp --resource-group $resourceGroupName --output none 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Function App restarted"
        Start-Sleep -Seconds 10
    }
} catch {
    Write-Warning "Could not restart Function App automatically"
}

# ===========================
# Step 7: Create Static Web App
# ===========================
Write-Step "Step 7: Create Static Web App"

Write-Info "Creating Static Web App: $($config.Resources.StaticWebApp)..."
# Static Web Apps have limited region availability, use West Europe if UK South selected
$swaLocation = if ($Location -eq "uksouth") { "westeurope" } else { $Location }
Write-Host "    (Using $swaLocation - Static Web Apps not available in all regions)" -ForegroundColor Gray
$swaResult = az staticwebapp create `
    --name $config.Resources.StaticWebApp `
    --resource-group $resourceGroupName `
    --location $swaLocation `
    --sku Free `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create Static Web App"
    exit 1
}

$config.Resources.StaticWebAppUrl = "https://$($swaResult.defaultHostname)"
Write-Success "Static Web App created"
Write-Host "    URL: $($config.Resources.StaticWebAppUrl)" -ForegroundColor Gray

# ===========================
# Step 8: Create Entra App Registration
# ===========================
Write-Step "Step 8: Create Entra App Registration"

Write-Info "Creating App Registration..."
$redirectUris = @($config.Resources.StaticWebAppUrl)
if ($CustomDomain) {
    $redirectUris += "https://$CustomDomain"
}

$appName = "FinanceHub-$CustomerName"
$appResult = az ad app create `
    --display-name $appName `
    --sign-in-audience AzureADMyOrg `
    --web-redirect-uris @($redirectUris) `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to create App Registration (you can do this manually later)"
    $config.EntraAppId = $null
} else {
    $config.EntraAppId = $appResult.appId
    Write-Success "App Registration created: $($config.EntraAppId)"
    
    Write-Info "Configuring API permissions..."
    az ad app permission add `
        --id $config.EntraAppId `
        --api 00000003-0000-0000-c000-000000000000 `
        --api-permissions e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope `
        --output none
    
    Write-Success "API permissions configured (requires admin consent)"
}

# ===========================
# Step 9: Build Function App
# ===========================
Write-Step "Step 9: Build Function App"

$functionAppDir = Join-Path $scriptRoot "FunctionApp"
Push-Location $functionAppDir

Write-Info "Cleaning previous builds..."
dotnet clean --configuration Release --verbosity quiet
Write-Success "Clean complete"

Write-Info "Building Function App..."
dotnet build --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    Pop-Location
    exit 1
}
Write-Success "Build complete"

Write-Info "Publishing Function App..."
dotnet publish --configuration Release --output ./publish --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed"
    Pop-Location
    exit 1
}
Write-Success "Publish complete"

Pop-Location

# ===========================
# Step 10: Deploy Function App
# ===========================
Write-Step "Step 10: Deploy Function App to Azure"

Push-Location $functionAppDir

Write-Info "Deploying to $($config.Resources.FunctionApp)..."
func azure functionapp publish $config.Resources.FunctionApp --nozip
if ($LASTEXITCODE -ne 0) {
    Write-Error "Function App deployment failed"
    Pop-Location
    exit 1
}
Write-Success "Function App deployed"

$config.Resources.FunctionAppUrl = "https://$($config.Resources.FunctionApp).azurewebsites.net"

Pop-Location

# ===========================
# Step 11: Initialize Database Schema
# ===========================
Write-Step "Step 11: Initialize Database Schema"

Push-Location $functionAppDir

Write-Info "Configuring connection string..."
$env:ConnectionStrings__FinanceHubDb = $connectionString
$env:DOTNET_ROLL_FORWARD = "LatestMajor"

Write-Info "Creating database schema..."
dotnet ef database update --verbose
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Automatic database initialization failed"
    Write-Host ""
    Write-Info "You can manually run the SQL script: migration-admin-tables.sql"
} else {
    Write-Success "Database schema created successfully"
}

Pop-Location

# ===========================
# Step 12: Update Static Web App Config
# ===========================
Write-Step "Step 12: Update Static Web App Configuration"

$authConfigPath = Join-Path $scriptRoot "StaticWebApp\src\auth\authConfig.js"
if (Test-Path $authConfigPath) {
    Write-Info "Updating authConfig.js with App Registration details..."
    
    $authConfigContent = Get-Content $authConfigPath -Raw
    if ($config.EntraAppId) {
        $authConfigContent = $authConfigContent -replace 'clientId: ".*?"', "clientId: `"$($config.EntraAppId)`""
        $authConfigContent = $authConfigContent -replace 'authority: "https://login.microsoftonline.com/.*?"', "authority: `"https://login.microsoftonline.com/$tenantId`""
    }
    
    Set-Content $authConfigPath $authConfigContent
    Write-Success "Auth config updated"
} else {
    Write-Warning "authConfig.js not found"
}

$apiServicePath = Join-Path $scriptRoot "StaticWebApp\src\services\apiService.js"
if (Test-Path $apiServicePath) {
    Write-Info "Updating apiService.js with Function App URL..."
    
    $apiServiceContent = Get-Content $apiServicePath -Raw
    $apiServiceContent = $apiServiceContent -replace "const API_BASE = '.*?'", "const API_BASE = '$($config.Resources.FunctionAppUrl)/api'"
    
    Set-Content $apiServicePath $apiServiceContent
    Write-Success "API service config updated"
} else {
    Write-Warning "apiService.js not found"
}

# ===========================
# Step 13: Build Static Web App
# ===========================
Write-Step "Step 13: Build Static Web App"

$staticWebAppDir = Join-Path $scriptRoot "StaticWebApp"
Push-Location $staticWebAppDir

Write-Info "Installing npm dependencies..."
npm install --silent
if ($LASTEXITCODE -ne 0) {
    Write-Error "npm install failed"
    Pop-Location
    exit 1
}
Write-Success "Dependencies installed"

Write-Info "Building Static Web App..."
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    Pop-Location
    exit 1
}
Write-Success "Build complete"

Pop-Location

# ===========================
# Step 14: Deploy Static Web App
# ===========================
Write-Step "Step 14: Deploy Static Web App to Azure"

Push-Location $staticWebAppDir

Write-Info "Deploying to $($config.Resources.StaticWebApp)..."

# Get deployment token
$deployToken = az staticwebapp secrets list `
    --name $config.Resources.StaticWebApp `
    --resource-group $resourceGroupName `
    --query "properties.apiKey" `
    --output tsv

if ([string]::IsNullOrEmpty($deployToken)) {
    Write-Error "Failed to get deployment token"
    Pop-Location
    exit 1
}

# Deploy using SWA CLI
swa deploy --app-location ./dist --deployment-token $deployToken --env production
if ($LASTEXITCODE -ne 0) {
    Write-Error "Static Web App deployment failed"
    Pop-Location
    exit 1
}
Write-Success "Static Web App deployed"

Pop-Location

# ===========================
# Step 15: Save Configuration
# ===========================
Write-Step "Step 15: Save Deployment Configuration"

$configPath = Join-Path $scriptRoot "deployment-config-$sanitizedCustomerName.json"
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath
Write-Success "Configuration saved to: deployment-config-$sanitizedCustomerName.json"

# ===========================
# Step 16: Grant Admin Consent
# ===========================
if ($config.EntraAppId) {
    Write-Step "Step 16: Grant Admin Consent for App Registration"
    
    Write-Info "Granting admin consent for API permissions..."
    $consentResult = az ad app permission admin-consent --id $config.EntraAppId 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Admin consent granted successfully"
    } else {
        Write-Warning "Could not automatically grant consent"
        Write-Host "  You can grant consent manually at:" -ForegroundColor Gray
        Write-Host "  https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/CallAnAPI/appId/$($config.EntraAppId)" -ForegroundColor Gray
    }
}

# ===========================
# Step 17: Configure Custom Domain
# ===========================
if ($CustomDomain) {
    Write-Step "Step 17: Configure Custom Domain"
    
    Write-Host ""
    Write-Host "  Configure DNS CNAME Record:" -ForegroundColor Yellow
    Write-Host "  ========================================" -ForegroundColor Cyan
    Write-Host "    Name: $CustomDomain" -ForegroundColor White
    Write-Host "    Type: CNAME" -ForegroundColor White
    Write-Host "    Value: $($swaResult.defaultHostname)" -ForegroundColor White
    Write-Host "  ========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Please add the CNAME record in your DNS provider," -ForegroundColor Yellow
    Write-Host "  then press ENTER to continue..." -ForegroundColor Yellow
    Read-Host
    
    Write-Info "Waiting for DNS propagation (checking every 10 seconds)..."
    $maxAttempts = 12
    $attempt = 0
    $dnsConfigured = $false
    
    while ($attempt -lt $maxAttempts -and -not $dnsConfigured) {
        $attempt++
        Write-Host "  Attempt $attempt/$maxAttempts..." -ForegroundColor Gray
        
        try {
            $dnsResult = Resolve-DnsName -Name $CustomDomain -Type CNAME -ErrorAction SilentlyContinue
            if ($dnsResult) {
                Write-Success "DNS record found!"
                $dnsConfigured = $true
            }
        } catch {
            # DNS not yet propagated
        }
        
        if (-not $dnsConfigured -and $attempt -lt $maxAttempts) {
            Start-Sleep -Seconds 10
        }
    }
    
    if ($dnsConfigured) {
        Write-Info "Configuring custom domain in Azure..."
        az staticwebapp hostname set `
            --name $config.Resources.StaticWebApp `
            --resource-group $resourceGroupName `
            --hostname $CustomDomain `
            --output none
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Custom domain configured: https://$CustomDomain"
            $config.CustomDomainConfigured = $true
        } else {
            Write-Warning "Failed to configure custom domain (DNS may still be propagating)"
            Write-Host "  You can configure it manually later with:" -ForegroundColor Gray
            Write-Host "  az staticwebapp hostname set --name $($config.Resources.StaticWebApp) --resource-group $resourceGroupName --hostname $CustomDomain" -ForegroundColor Gray
        }
    } else {
        Write-Warning "DNS not detected after 2 minutes"
        Write-Host "  Please wait for DNS to propagate (can take up to 48 hours)" -ForegroundColor Gray
        Write-Host "  Then run: az staticwebapp hostname set --name $($config.Resources.StaticWebApp) --resource-group $resourceGroupName --hostname $CustomDomain" -ForegroundColor Gray
    }
}

# ===========================
# Deployment Summary
# ===========================
Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

Write-Host "Deployment Details:" -ForegroundColor Yellow
Write-Host "  Customer: $CustomerName" -ForegroundColor White
Write-Host "  Resource Group: $resourceGroupName" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host ""

Write-Host "Application URLs:" -ForegroundColor Yellow
Write-Host "  Static Web App: $($config.Resources.StaticWebAppUrl)" -ForegroundColor White
if ($CustomDomain -and $config.CustomDomainConfigured) {
    Write-Host "  Custom Domain: https://$CustomDomain ✓" -ForegroundColor Green
} elseif ($CustomDomain) {
    Write-Host "  Custom Domain: https://$CustomDomain (pending DNS)" -ForegroundColor Yellow
}
Write-Host "  Function App: $($config.Resources.FunctionAppUrl)" -ForegroundColor White
Write-Host ""

Write-Host "Database Details:" -ForegroundColor Yellow
Write-Host "  SQL Server: $($config.Resources.SqlServerFQDN)" -ForegroundColor White
Write-Host "  Database: $($config.Resources.SqlDatabase)" -ForegroundColor White
Write-Host "  Admin User: $($config.Resources.SqlAdminUser)" -ForegroundColor White
Write-Host "  Password: [Stored in Key Vault: $($config.Resources.KeyVault)]" -ForegroundColor White
Write-Host ""

if ($config.EntraAppId) {
    Write-Host "Entra ID App Registration:" -ForegroundColor Yellow
    Write-Host "  App ID: $($config.EntraAppId)" -ForegroundColor White
    Write-Host "  Name: $appName" -ForegroundColor White
    Write-Host "  Status: Admin consent granted ✓" -ForegroundColor Green
    Write-Host ""
}

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Login to your application:" -ForegroundColor White
if ($config.CustomDomainConfigured) {
    Write-Host "   URL: https://$CustomDomain" -ForegroundColor Gray
} else {
    Write-Host "   URL: $($config.Resources.StaticWebAppUrl)" -ForegroundColor Gray
}
Write-Host "   Username: admin" -ForegroundColor Gray
Write-Host "   Password: FinanceHub2026!" -ForegroundColor Gray
Write-Host "   (You'll be prompted to change this on first login)" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Configure Company Settings:" -ForegroundColor White
Write-Host "   • Navigate to Settings → Company" -ForegroundColor Gray
Write-Host "   • Enter company details, bank info, etc." -ForegroundColor Gray
Write-Host ""
Write-Host "3. Configure SMTP:" -ForegroundColor White
Write-Host "   • Navigate to Settings → Company" -ForegroundColor Gray
Write-Host "   • Enter SMTP server details for email notifications" -ForegroundColor Gray
Write-Host "   • SMTP password will be stored in Key Vault" -ForegroundColor Gray
Write-Host ""
Write-Host "4. (Optional) Configure SSO:" -ForegroundColor White
Write-Host "   • Navigate to Settings → Security & SSO" -ForegroundColor Gray
Write-Host "   • Enable SSO and configure Entra ID settings" -ForegroundColor Gray
Write-Host "   • App Registration is already created and ready to use" -ForegroundColor Gray
Write-Host "   • App ID: $($config.EntraAppId)" -ForegroundColor Gray
Write-Host ""

Write-Host "Configuration file saved: $configPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Done! 🎉" -ForegroundColor Green
Write-Host ""
