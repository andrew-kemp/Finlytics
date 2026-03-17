<#
    04-Configure-Azure-Resources.ps1
    Gathers Azure subscription configuration and provisions all resources
    for the FinanceHub solution (Function App, Logic App, Static Web App)
#>

param(
    [switch]$SkipLogin
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

# ===========================
# INI HELPERS
# ===========================
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

function Set-IniValue {
    param([string]$Path,[string]$Section,[string]$Key,[string]$Value)
    if (-not (Test-Path $Path)) { "# FinanceHub Configuration" | Set-Content -Path $Path -Encoding UTF8 }
    $ini = Get-IniContent -Path $Path
    if (-not $ini.ContainsKey($Section)) { $ini[$section] = @{} }
    $ini[$Section][$Key] = $Value
    
    $sb = New-Object System.Text.StringBuilder
    foreach ($sec in $ini.Keys) {
        [void]$sb.AppendLine("[$sec]")
        foreach ($k in $ini[$sec].Keys) { [void]$sb.AppendLine("$k=$($ini[$sec][$k])") }
        [void]$sb.AppendLine("")
    }
    $sb.ToString() | Set-Content -Path $Path -Encoding UTF8
}

function Get-IniValueOrPrompt {
    param(
        [string]$Path,[string]$Section,[string]$Key,
        [string]$Prompt,[string]$Default = "",
        [switch]$AllowBlank
    )
    $ini = Get-IniContent -Path $Path
    if (-not $ini.ContainsKey($Section)) { $ini[$Section] = @{} }
    
    $value = $null
    if ($ini[$Section].ContainsKey($Key)) { $value = $ini[$Section][$Key] }
    
    if ([string]::IsNullOrWhiteSpace($value)) {
        if ([string]::IsNullOrWhiteSpace($Default)) {
            $value = Read-Host $Prompt
        } else {
            $input = Read-Host "$Prompt [$Default]"
            $value = if ([string]::IsNullOrWhiteSpace($input)) { $Default } else { $input }
        }
        
        if (-not $AllowBlank) {
            while ([string]::IsNullOrWhiteSpace($value)) {
                $value = Read-Host $Prompt
            }
        }
        Set-IniValue -Path $Path -Section $Section -Key $Key -Value $value
    }
    return $value
}

function Ensure-Module {
    param([string]$Name,[string]$MinVersion="0.0.0")
    $installed = Get-Module -ListAvailable -Name $Name | Where-Object { $_.Version -ge [version]$MinVersion } | Select-Object -First 1
    if (-not $installed) {
        Write-Host "Installing $Name (min $MinVersion)..." -ForegroundColor Yellow
        Install-Module -Name $Name -MinimumVersion $MinVersion -Scope CurrentUser -Force -AllowClobber
    } else {
        Write-Host "✓ $Name v$($installed.Version) already installed" -ForegroundColor Green
    }
}

try {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "FinanceHub Azure Resource Configuration" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    if (-not (Test-Path $configFile)) {
        throw "Config file not found: $configFile. Please run FinanceHubProvisioner.ps1 first."
    }
    
    $config = Get-IniContent -Path $configFile
    
    # ===========================
    # Part 1: Install Required Modules
    # ===========================
    Write-Host "--- Installing Azure Modules ---`n" -ForegroundColor Cyan
    
    $modules = @(
        @{Name="Az.Accounts"; MinVersion="2.0.0"}
        @{Name="Az.Resources"; MinVersion="6.0.0"}
        @{Name="Az.Functions"; MinVersion="4.0.0"}
        @{Name="Az.Storage"; MinVersion="5.0.0"}
        @{Name="Az.Websites"; MinVersion="3.0.0"}
    )
    
    foreach ($module in $modules) {
        Write-Host "Checking module: $($module.Name)..." -ForegroundColor Yellow
        Ensure-Module -Name $module.Name -MinVersion $module.MinVersion
    }
    
    # ===========================
    # Part 2: Azure Login & Subscription
    # ===========================
    Write-Host "`n--- Azure Authentication ---`n" -ForegroundColor Cyan
    
    if (-not $SkipLogin) {
        Write-Host "Connecting to Azure..." -ForegroundColor Yellow
        Write-Host "Browser authentication will open.`n" -ForegroundColor Gray
        
        # Disconnect any existing session for clean login
        try {
            Disconnect-AzAccount -ErrorAction SilentlyContinue | Out-Null
        } catch {}
        
        Connect-AzAccount -TenantId $config["Tenant"]["TenantId"]
        $azContext = Get-AzContext
        
        if ($null -eq $azContext) {
            throw "Failed to connect to Azure"
        }
        
        Write-Host "✓ Connected to Azure as $($azContext.Account.Id)" -ForegroundColor Green
    } else {
        $azContext = Get-AzContext
        if ($null -eq $azContext) {
            throw "Not connected to Azure. Remove -SkipLogin flag or run Connect-AzAccount first."
        }
        Write-Host "✓ Using existing Azure connection" -ForegroundColor Green
    }
    
    # Handle subscription selection
    Write-Host "`n--- Subscription Selection ---`n" -ForegroundColor Cyan
    $subscriptions = Get-AzSubscription
    
    if ($subscriptions.Count -gt 1) {
        Write-Host "Available subscriptions:" -ForegroundColor Cyan
        for ($i = 0; $i -lt $subscriptions.Count; $i++) {
            $marker = if ($subscriptions[$i].Id -eq $azContext.Subscription.Id) { " (current)" } else { "" }
            Write-Host "  [$i] $($subscriptions[$i].Name)$marker" -ForegroundColor Gray
        }
        Write-Host "`nCurrent: $($azContext.Subscription.Name)" -ForegroundColor Yellow
        $selection = Read-Host "Select subscription number (or press Enter to keep current)"
        
        if (-not [string]::IsNullOrWhiteSpace($selection)) {
            $selectedSub = $subscriptions[$selection]
            Set-AzContext -SubscriptionId $selectedSub.Id | Out-Null
            $azContext = Get-AzContext
            Write-Host "✓ Switched to: $($selectedSub.Name)" -ForegroundColor Green
        }
    } else {
        Write-Host "Using subscription: $($azContext.Subscription.Name)" -ForegroundColor Gray
    }
    
    # Save subscription details
    Set-IniValue -Path $configFile -Section "Azure" -Key "SubscriptionId" -Value $azContext.Subscription.Id
    Set-IniValue -Path $configFile -Section "Azure" -Key "SubscriptionName" -Value $azContext.Subscription.Name
    Set-IniValue -Path $configFile -Section "Azure" -Key "TenantId" -Value $azContext.Tenant.Id
    
    Write-Host "`n✓ Subscription configured:" -ForegroundColor Green
    Write-Host "  Name: $($azContext.Subscription.Name)" -ForegroundColor Gray
    Write-Host "  ID: $($azContext.Subscription.Id)" -ForegroundColor Gray
    
    # ===========================
    # Part 3: Azure Resource Configuration
    # ===========================
    Write-Host "`n--- Azure Resource Names ---`n" -ForegroundColor Cyan
    Write-Host "Configure names for Azure resources." -ForegroundColor Gray
    Write-Host "Press Enter to accept defaults shown in [brackets].`n" -ForegroundColor Gray
    
    # Resource Group
    $rgName = Get-IniValueOrPrompt -Path $configFile -Section "Azure" -Key "ResourceGroup" `
        -Prompt "Resource Group name" -Default "rg-financehub-prod"
    
    # Region
    $region = Get-IniValueOrPrompt -Path $configFile -Section "Azure" -Key "Location" `
        -Prompt "Azure region" -Default "uksouth"
    
    # Generate unique suffixes for global resources
    $uniqueSuffix = Get-Random -Minimum 1000 -Maximum 9999
    
    # Function App
    $funcAppDefault = "func-financehub-$uniqueSuffix"
    $functionAppName = Get-IniValueOrPrompt -Path $configFile -Section "Azure" -Key "FunctionAppName" `
        -Prompt "Function App name" -Default $funcAppDefault
    
    # Static Web App
    $swaDefault = "swa-financehub-$uniqueSuffix"
    $staticWebAppName = Get-IniValueOrPrompt -Path $configFile -Section "Azure" -Key "StaticWebAppName" `
        -Prompt "Static Web App name" -Default $swaDefault
    
    # Logic App
    $logicDefault = "logic-financehub-reminders"
    $logicAppName = Get-IniValueOrPrompt -Path $configFile -Section "Azure" -Key "LogicAppName" `
        -Prompt "Logic App name" -Default $logicDefault
    
    # Storage Account (must be globally unique, lowercase, no hyphens)
    $storageDefault = "stfinancehub$uniqueSuffix"
    $storageAccountName = Get-IniValueOrPrompt -Path $configFile -Section "Azure" -Key "StorageAccountName" `
        -Prompt "Storage Account name (lowercase, no special chars)" -Default $storageDefault
    $storageAccountName = $storageAccountName.ToLower() -replace '[^a-z0-9]', ''
    Set-IniValue -Path $configFile -Section "Azure" -Key "StorageAccountName" -Value $storageAccountName
    
    # App Service Plan
    $planDefault = "asp-financehub-prod"
    $appServicePlanName = Get-IniValueOrPrompt -Path $configFile -Section "Azure" -Key "AppServicePlanName" `
        -Prompt "App Service Plan name" -Default $planDefault
    
    # ===========================
    # Part 4: Email Configuration
    # ===========================
    Write-Host "`n--- Email Configuration ---`n" -ForegroundColor Cyan
    
    $quotesEmail = Get-IniValueOrPrompt -Path $configFile -Section "Email" -Key "QuotesFromEmail" `
        -Prompt "Quotes sender email" -Default "quotes@andykemp.com"
    
    $invoicesEmail = Get-IniValueOrPrompt -Path $configFile -Section "Email" -Key "InvoicesFromEmail" `
        -Prompt "Invoices sender email" -Default "invoices@andykemp.com"
    
    $reminderCC = Get-IniValueOrPrompt -Path $configFile -Section "Email" -Key "ReminderCCEmail" `
        -Prompt "Reminder CC email" -Default "andrew@andykemp.com"
    
    # ===========================
    # Part 5: Logic App Schedule
    # ===========================
    Write-Host "`n--- Logic App Schedule ---`n" -ForegroundColor Cyan
    Write-Host "Cron format: 0 0 9 */2 * * = 9am every 2 days" -ForegroundColor Gray
    
    $schedule = Get-IniValueOrPrompt -Path $configFile -Section "LogicApp" -Key "ReminderSchedule" `
        -Prompt "Reminder schedule (cron)" -Default "0 0 9 */2 * *"
    
    $daysOverdue = Get-IniValueOrPrompt -Path $configFile -Section "LogicApp" -Key "ReminderDaysOverdue" `
        -Prompt "Days overdue threshold" -Default "7"
    
    # ===========================
    # Part 6: Provision Azure Resources
    # ===========================
    Write-Host "`n--- Provisioning Azure Resources ---`n" -ForegroundColor Cyan
    Write-Host "This will create the following resources:" -ForegroundColor Yellow
    Write-Host "  • Resource Group: $rgName" -ForegroundColor Gray
    Write-Host "  • Storage Account: $storageAccountName" -ForegroundColor Gray
    Write-Host "  • App Service Plan: $appServicePlanName" -ForegroundColor Gray
    Write-Host "  • Function App: $functionAppName" -ForegroundColor Gray
    Write-Host "  • Static Web App: $staticWebAppName" -ForegroundColor Gray
    Write-Host "  • Logic App: $logicAppName`n" -ForegroundColor Gray
    
    $confirm = Read-Host "Proceed with provisioning? (Y/N)"
    if ($confirm -ne "Y") {
        Write-Host "Provisioning cancelled." -ForegroundColor Yellow
        exit 0
    }
    
    # Create Resource Group
    Write-Host "`nCreating Resource Group: $rgName..." -ForegroundColor Yellow
    $rg = Get-AzResourceGroup -Name $rgName -ErrorAction SilentlyContinue
    if (-not $rg) {
        New-AzResourceGroup -Name $rgName -Location $region | Out-Null
        Write-Host "✓ Resource Group created" -ForegroundColor Green
    } else {
        Write-Host "✓ Resource Group already exists" -ForegroundColor Green
    }
    
    # Create Storage Account
    Write-Host "Creating Storage Account: $storageAccountName..." -ForegroundColor Yellow
    $storage = Get-AzStorageAccount -ResourceGroupName $rgName -Name $storageAccountName -ErrorAction SilentlyContinue
    if (-not $storage) {
        New-AzStorageAccount -ResourceGroupName $rgName -Name $storageAccountName `
            -Location $region -SkuName Standard_LRS -Kind StorageV2 | Out-Null
        Write-Host "✓ Storage Account created" -ForegroundColor Green
    } else {
        Write-Host "✓ Storage Account already exists" -ForegroundColor Green
    }
    
    # Create App Service Plan (Consumption for cost-effectiveness)
    Write-Host "Creating App Service Plan: $appServicePlanName..." -ForegroundColor Yellow
    $plan = Get-AzAppServicePlan -ResourceGroupName $rgName -Name $appServicePlanName -ErrorAction SilentlyContinue
    if (-not $plan) {
        New-AzAppServicePlan -ResourceGroupName $rgName -Name $appServicePlanName `
            -Location $region -Tier Dynamic -WorkerSize Small | Out-Null
        Write-Host "✓ App Service Plan created (Consumption)" -ForegroundColor Green
    } else {
        Write-Host "✓ App Service Plan already exists" -ForegroundColor Green
    }
    
    # Create Function App
    Write-Host "Creating Function App: $functionAppName..." -ForegroundColor Yellow
    $func = Get-AzFunctionApp -ResourceGroupName $rgName -Name $functionAppName -ErrorAction SilentlyContinue
    if (-not $func) {
        New-AzFunctionApp -ResourceGroupName $rgName -Name $functionAppName -Location $region `
            -StorageAccountName $storageAccountName -Runtime dotnet -RuntimeVersion 8 `
            -FunctionsVersion 4 -OSType Windows | Out-Null
        Write-Host "✓ Function App created" -ForegroundColor Green
    } else {
        Write-Host "✓ Function App already exists" -ForegroundColor Green
    }
    
    # Configure Function App Settings
    Write-Host "Configuring Function App settings..." -ForegroundColor Yellow
    $appSettings = @{
        "SharePointSiteUrl" = $config["SharePoint"]["SiteUrl"]
        "TenantId" = $config["Tenant"]["TenantId"]
        "ClientId" = $config["App"]["ClientId"]
        "CertificateThumbprint" = $config["App"]["Thumbprint"]
        "BaseCurrency" = $config["Finance"]["BaseCurrency"]
        "QuotesFromEmail" = $quotesEmail
        "InvoicesFromEmail" = $invoicesEmail
    }
    Update-AzFunctionAppSetting -ResourceGroupName $rgName -Name $functionAppName `
        -AppSetting $appSettings -Force | Out-Null
    Write-Host "✓ Function App settings configured" -ForegroundColor Green
    
    # Save Function App URL
    $funcUrl = "https://$functionAppName.azurewebsites.net"
    Set-IniValue -Path $configFile -Section "FunctionApp" -Key "FunctionAppUrl" -Value $funcUrl
    
    # Create Static Web App
    Write-Host "Creating Static Web App: $staticWebAppName..." -ForegroundColor Yellow
    $swa = Get-AzStaticWebApp -ResourceGroupName $rgName -Name $staticWebAppName -ErrorAction SilentlyContinue
    if (-not $swa) {
        # Static Web Apps not available in all regions - use westeurope if uksouth
        $swaRegion = if ($region -eq "uksouth") { "westeurope" } else { $region }
        Write-Host "  Using region: $swaRegion (Static Web Apps not available in all regions)" -ForegroundColor Gray
        New-AzStaticWebApp -ResourceGroupName $rgName -Name $staticWebAppName `
            -Location $swaRegion -SkuName Free | Out-Null
        Write-Host "✓ Static Web App created" -ForegroundColor Green
        Write-Host "  Note: Connect to your GitHub repo in Azure Portal for CI/CD" -ForegroundColor Cyan
    } else {
        Write-Host "✓ Static Web App already exists" -ForegroundColor Green
    }
    
    # Get Static Web App URL
    $swaDetails = Get-AzStaticWebApp -ResourceGroupName $rgName -Name $staticWebAppName
    $swaUrl = "https://$($swaDetails.DefaultHostname)"
    Set-IniValue -Path $configFile -Section "StaticWebApp" -Key "StaticWebAppUrl" -Value $swaUrl
    
    # Create Logic App placeholder (requires manual workflow definition)
    Write-Host "Creating Logic App: $logicAppName..." -ForegroundColor Yellow
    Write-Host "  Note: Logic App workflow must be configured in Azure Portal" -ForegroundColor Cyan
    Write-Host "  A workflow definition template will be generated in the next step" -ForegroundColor Cyan
    
    # Save Logic App name for reference
    Set-IniValue -Path $configFile -Section "LogicApp" -Key "LogicAppName" -Value $logicAppName
    
    # ===========================
    # Part 7: Summary
    # ===========================
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Azure Resources Provisioned Successfully" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    Write-Host "Resource Group: $rgName" -ForegroundColor White
    Write-Host "  Location: $region" -ForegroundColor Gray
    Write-Host "`nFunction App:" -ForegroundColor White
    Write-Host "  Name: $functionAppName" -ForegroundColor Gray
    Write-Host "  URL: $funcUrl" -ForegroundColor Gray
    Write-Host "`nStatic Web App:" -ForegroundColor White
    Write-Host "  Name: $staticWebAppName" -ForegroundColor Gray
    Write-Host "  URL: $swaUrl" -ForegroundColor Gray
    Write-Host "`nLogic App: $logicAppName" -ForegroundColor White
    Write-Host "`nStorage Account: $storageAccountName" -ForegroundColor White
    
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Next Steps" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    Write-Host "1. Run 05-Generate-Function-Code.ps1 to create Function App code" -ForegroundColor Yellow
    Write-Host "2. Run 06-Generate-StaticWebApp-Code.ps1 to create web app" -ForegroundColor Yellow
    Write-Host "3. Run 07-Generate-LogicApp-Workflow.ps1 to create Logic App definition" -ForegroundColor Yellow
    Write-Host "4. Deploy code using provided deployment scripts`n" -ForegroundColor Yellow
    
    Write-Host "✓ Configuration complete!`n" -ForegroundColor Green
    
    exit 0
}
catch {
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    try { Disconnect-AzAccount -ErrorAction SilentlyContinue | Out-Null } catch {}
    exit 1
}