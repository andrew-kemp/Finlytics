<#
    01a-Pre-Requisites.ps1
    Gathers ALL configuration for FinanceHub deployment
    
    Collects:
      - Tenant information
      - Company branding
      - Regional settings (currency, timezone, locale)
      - Email addresses for quotes and invoices
      - Azure subscription details
      - Resource naming preferences
      - SharePoint site details
      - Shared mailbox user access
    
    Saves everything to finance-hub-config.ini for use by subsequent scripts
    Requires: PowerShell 7+
#>

param()

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

# ---------------------------
# INI helpers
# ---------------------------
function Get-IniContent {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @{} }
    $ini = @{}
    $section = ""
    switch -regex -file $Path {
        "^\s*\[(.+)\]\s*$" {
            $section = $matches[1]
            $ini[$section] = @{}
        }
        "^\s*([^=]+?)\s*=\s*(.*)$" {
            if (-not $section) { $section = "Default"; $ini[$section] = @{} }
            $name = $matches[1].Trim()
            $value = $matches[2]
            $ini[$section][$name] = $value
        }
    }
    return $ini
}

function Set-IniValue {
    param([string]$Path,[string]$Section,[string]$Key,[string]$Value)
    if (-not (Test-Path $Path)) { "" | Set-Content $Path }
    $ini = Get-IniContent $Path
    if (-not $ini.ContainsKey($Section)) { $ini[$Section] = @{} }
    $ini[$Section][$Key] = $Value

    $sb = New-Object System.Text.StringBuilder
    foreach ($sec in $ini.Keys) {
        [void]$sb.AppendLine("[$sec]")
        foreach ($k in $ini[$sec].Keys) {
            [void]$sb.AppendLine("$k=$($ini[$sec][$k])")
        }
        [void]$sb.AppendLine("")
    }
    $sb.ToString() | Set-Content -Path $Path -Encoding UTF8
}

function Get-IniValueOrPrompt {
    param(
        [string]$Section,[string]$Key,[string]$Prompt,[string]$Default = ""
    )
    $ini = Get-IniContent $configFile
    $current = $null
    if ($ini.ContainsKey($Section)) { $current = $ini[$Section][$Key] }
    if ([string]::IsNullOrWhiteSpace($current)) {
        if ($Default) {
            $input = Read-Host "$Prompt [$Default]"
            $current = if ($input) { $input } else { $Default }
        } else {
            $current = Read-Host $Prompt
        }
        Set-IniValue -Path $configFile -Section $Section -Key $Key -Value $current
    }
    return $current
}

function Read-Choice {
    param(
        [string]$PromptText,
        [string[]]$Options,
        [string]$Default
    )
    $opts = $Options -join '/'
    $inp = Read-Host "$PromptText [$opts] (default $Default)"
    if ([string]::IsNullOrWhiteSpace($inp)) { return $Default }
    foreach ($o in $Options) {
        if ($inp.Trim().ToLower() -eq $o.ToLower()) { return $o }
    }
    return $Default
}

function New-Slug {
    param([string]$Text)
    $t = $Text.ToLowerInvariant()
    $t = $t -replace "[^a-z0-9]+","-"
    $t.Trim("-")
}

function Get-InitialsFromName {
    param([string]$Name,[int]$MaxLen=5)
    if ([string]::IsNullOrWhiteSpace($Name)) { return "AK" }
    $simplified = ($Name -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
    if ($simplified.Length -le $MaxLen -and $Name -match '^[A-Za-z0-9]+$') {
        return $simplified
    }
    $words = $Name -split '[\s\-_,]+' | Where-Object { $_ -ne '' }
    $initials = ($words | ForEach-Object { $_.Substring(0,1) }) -join ''
    $initials = $initials.ToUpperInvariant()
    if ($initials.Length -gt $MaxLen) { $initials = $initials.Substring(0,$MaxLen) }
    if ([string]::IsNullOrWhiteSpace($initials)) { $initials = "AK" }
    return $initials
}

# ---------------------------
# Banner
# ---------------------------
Clear-Host
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " FinanceHub Pre-Requisites Configuration" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will gather ALL configuration needed for FinanceHub deployment." -ForegroundColor Yellow
Write-Host "All values will be saved to: $configFile" -ForegroundColor Yellow
Write-Host ""
Write-Host "Press Ctrl+C to abort at any time." -ForegroundColor Gray
Write-Host ""

# ---------------------------
# Section 1: Tenant Information
# ---------------------------
Write-Host ""
Write-Host "=== SECTION 1: Tenant Information ===" -ForegroundColor Green
Write-Host ""

$tenantShort = Get-IniValueOrPrompt -Section "Tenant" -Key "ShortName" -Prompt "Tenant short name (e.g., kempy)"

# Derive all tenant values from short name
$tenantId = "$tenantShort.onmicrosoft.com"
Set-IniValue -Path $configFile -Section "Tenant" -Key "TenantId" -Value $tenantId
$rootUrl = "https://$tenantShort.sharepoint.com"
Set-IniValue -Path $configFile -Section "Tenant" -Key "RootUrl" -Value $rootUrl
$adminUrl = "https://$tenantShort-admin.sharepoint.com"
Set-IniValue -Path $configFile -Section "Tenant" -Key "AdminUrl" -Value $adminUrl

Write-Host "  ✓ Tenant ID: $tenantId" -ForegroundColor Gray
Write-Host "  ✓ SharePoint Root: $rootUrl" -ForegroundColor Gray
Write-Host "  ✓ SharePoint Admin: $adminUrl" -ForegroundColor Gray

# ---------------------------
# Section 2: Company Branding
# ---------------------------
Write-Host ""
Write-Host "=== SECTION 2: Company Branding ===" -ForegroundColor Green
Write-Host ""

$companyName = Get-IniValueOrPrompt -Section "Brand" -Key "CompanyName" -Prompt "Your company name" -Default "AK"
$autoInitials = Get-InitialsFromName -Name $companyName -MaxLen 5
$companyInitials = Get-IniValueOrPrompt -Section "Brand" -Key "CompanyInitials" -Prompt "Company initials or prefix" -Default $autoInitials

$productSuffixDefault = (Get-IniValueOrPrompt -Section "Brand" -Key "ProductSuffix" -Prompt "Product name (LedgerHub or FinanceHub)" -Default "LedgerHub")
$productSuffix = Read-Choice -PromptText "Confirm product name" -Options @("LedgerHub","FinanceHub") -Default $productSuffixDefault
Set-IniValue -Path $configFile -Section "Brand" -Key "ProductSuffix" -Value $productSuffix

$brandShort = "$companyInitials $productSuffix"
$internalPrefix = "$companyInitials-" + ($productSuffix -replace 'Hub$','H')
Set-IniValue -Path $configFile -Section "Brand" -Key "BrandShort" -Value $brandShort
Set-IniValue -Path $configFile -Section "Brand" -Key "Prefix" -Value $internalPrefix

Write-Host "  ✓ Brand: $brandShort" -ForegroundColor Gray
Write-Host "  ✓ Internal Prefix: $internalPrefix" -ForegroundColor Gray

# ---------------------------
# Section 3: Regional Settings
# ---------------------------
Write-Host ""
Write-Host "=== SECTION 3: Regional Settings ===" -ForegroundColor Green
Write-Host ""

$currencyMap = @{
    "GBP" = @{ Code = "GBP"; Symbol = "£"; LCID = 2057; Name = "British Pound" }
    "USD" = @{ Code = "USD"; Symbol = "$"; LCID = 1033; Name = "US Dollar" }
    "EUR" = @{ Code = "EUR"; Symbol = "€"; LCID = 1031; Name = "Euro" }
}

$currencyChoice = Read-Choice -PromptText "Base currency" -Options @("GBP","USD","EUR") -Default "GBP"
$currency = $currencyMap[$currencyChoice]
Set-IniValue -Path $configFile -Section "Finance" -Key "BaseCurrency" -Value $currency.Code
Set-IniValue -Path $configFile -Section "Finance" -Key "CurrencySymbol" -Value $currency.Symbol
Set-IniValue -Path $configFile -Section "Finance" -Key "CurrencyLCID" -Value $currency.LCID

$localeChoice = Read-Choice -PromptText "Locale/Region" -Options @("UK","US","EU") -Default "UK"
$localeMap = @{
    "UK" = @{ LCID = 2057; TimeZone = "GMT Standard Time"; FirstDay = "Monday" }
    "US" = @{ LCID = 1033; TimeZone = "Eastern Standard Time"; FirstDay = "Sunday" }
    "EU" = @{ LCID = 1031; TimeZone = "W. Europe Standard Time"; FirstDay = "Monday" }
}
$locale = $localeMap[$localeChoice]
Set-IniValue -Path $configFile -Section "Regional" -Key "LocaleId" -Value $locale.LCID
Set-IniValue -Path $configFile -Section "Regional" -Key "TimeZone" -Value $locale.TimeZone
Set-IniValue -Path $configFile -Section "Regional" -Key "FirstDayOfWeek" -Value $locale.FirstDay
Set-IniValue -Path $configFile -Section "Regional" -Key "Use24HourClock" -Value "Y"

Write-Host "  ✓ Currency: $($currency.Name) ($($currency.Symbol))" -ForegroundColor Gray
Write-Host "  ✓ Locale: LCID $($locale.LCID)" -ForegroundColor Gray
Write-Host "  ✓ TimeZone: $($locale.TimeZone)" -ForegroundColor Gray

# ---------------------------
# Section 4: Email Configuration
# ---------------------------
Write-Host ""
Write-Host "=== SECTION 4: Email Configuration ===" -ForegroundColor Green
Write-Host ""

$domainDefault = ($tenantShort -eq "kempy") ? "andykemp.com" : "$tenantShort.com"
$emailDomain = Get-IniValueOrPrompt -Section "Email" -Key "Domain" -Prompt "Email domain for quotes/invoices" -Default $domainDefault

$quotesEmail = Get-IniValueOrPrompt -Section "Email" -Key "QuotesFromEmail" -Prompt "Quotes from email" -Default "quotes@$emailDomain"
$invoicesEmail = Get-IniValueOrPrompt -Section "Email" -Key "InvoicesFromEmail" -Prompt "Invoices from email" -Default "invoices@$emailDomain"
$reminderCCEmail = Get-IniValueOrPrompt -Section "Email" -Key "ReminderCCEmail" -Prompt "Your email (for CC on reminders)" -Default "andrew@$emailDomain"

# Shared mailbox users (will be used by 01b-EXO script)
$mailboxUsersDefault = $reminderCCEmail
$mailboxUsers = Get-IniValueOrPrompt -Section "Email" -Key "SharedMailboxUsers" -Prompt "Users who need access to shared mailboxes (comma-separated)" -Default $mailboxUsersDefault

Write-Host "  ✓ Quotes: $quotesEmail" -ForegroundColor Gray
Write-Host "  ✓ Invoices: $invoicesEmail" -ForegroundColor Gray
Write-Host "  ✓ Reminder CC: $reminderCCEmail" -ForegroundColor Gray
Write-Host "  ✓ Mailbox Access: $mailboxUsers" -ForegroundColor Gray

# ---------------------------
# Section 5: SharePoint Site
# ---------------------------
Write-Host ""
Write-Host "=== SECTION 5: SharePoint Site ===" -ForegroundColor Green
Write-Host ""

$siteTitle = Get-IniValueOrPrompt -Section "SharePoint" -Key "SiteTitle" -Prompt "SharePoint Site Title" -Default $brandShort
$siteAliasDefault = New-Slug $brandShort
$siteAlias = Get-IniValueOrPrompt -Section "SharePoint" -Key "SiteAlias" -Prompt "Site alias (/sites/<alias>)" -Default $siteAliasDefault
$siteUrl = "$rootUrl/sites/$siteAlias"
Set-IniValue -Path $configFile -Section "SharePoint" -Key "SiteUrl" -Value $siteUrl

$ownerUpnDefault = "$($tenantShort -eq 'kempy' ? 'andrew' : 'admin')@$($tenantShort -eq 'kempy' ? 'kemponline.co.uk' : "$tenantShort.onmicrosoft.com")"
$ownerUpn = Get-IniValueOrPrompt -Section "SharePoint" -Key "OwnerUpn" -Prompt "Site owner UPN" -Default $ownerUpnDefault

$siteType = Get-IniValueOrPrompt -Section "SharePoint" -Key "SiteType" -Prompt "Site type (Team or Communication)" -Default "Team"
if ($siteType -match "comm") {
    Write-Warning "Communication sites cannot be converted to Team sites later."
}

Write-Host "  ✓ Site URL: $siteUrl" -ForegroundColor Gray
Write-Host "  ✓ Site Owner: $ownerUpn" -ForegroundColor Gray

# ---------------------------
# Section 6: Azure Resources
# ---------------------------
Write-Host ""
Write-Host "=== SECTION 6: Azure Resources ===" -ForegroundColor Green
Write-Host ""

Write-Host "Azure subscription details will be gathered by script 02." -ForegroundColor Yellow
Write-Host "Setting default resource naming..." -ForegroundColor Yellow

$uniqueSuffix = Get-Random -Minimum 1000 -Maximum 9999
$rgName = Get-IniValueOrPrompt -Section "Azure" -Key "ResourceGroup" -Prompt "Resource Group name" -Default "rg-financehub-prod"
$locationDefault = "uksouth"
$location = Get-IniValueOrPrompt -Section "Azure" -Key "Location" -Prompt "Azure region" -Default $locationDefault

$funcAppName = Get-IniValueOrPrompt -Section "Azure" -Key "FunctionAppName" -Prompt "Function App name" -Default "func-financehub-$uniqueSuffix"
$swaName = Get-IniValueOrPrompt -Section "Azure" -Key "StaticWebAppName" -Prompt "Static Web App name" -Default "swa-financehub-$uniqueSuffix"
$logicAppName = Get-IniValueOrPrompt -Section "Azure" -Key "LogicAppName" -Prompt "Logic App name" -Default "logic-financehub-reminders"
$storageAccountName = Get-IniValueOrPrompt -Section "Azure" -Key "StorageAccountName" -Prompt "Storage Account name" -Default "stfinancehub$uniqueSuffix"

Write-Host "  ✓ Resource Group: $rgName" -ForegroundColor Gray
Write-Host "  ✓ Location: $location" -ForegroundColor Gray
Write-Host "  ✓ Function App: $funcAppName" -ForegroundColor Gray
Write-Host "  ✓ Static Web App: $swaName" -ForegroundColor Gray
Write-Host "  ✓ Logic App: $logicAppName" -ForegroundColor Gray

# ---------------------------
# Section 7: Logic App Schedule
# ---------------------------
Write-Host ""
Write-Host "=== SECTION 7: Logic App Reminder Schedule ===" -ForegroundColor Green
Write-Host ""

$scheduleChoice = Read-Choice -PromptText "Reminder frequency" -Options @("Daily","Every2Days","Weekly") -Default "Every2Days"
$scheduleMap = @{
    "Daily" = "0 0 9 * * *"
    "Every2Days" = "0 0 9 */2 * *"
    "Weekly" = "0 0 9 * * 1"
}
$schedule = $scheduleMap[$scheduleChoice]
Set-IniValue -Path $configFile -Section "LogicApp" -Key "ReminderSchedule" -Value $schedule
Set-IniValue -Path $configFile -Section "LogicApp" -Key "ReminderDaysOverdue" -Value "7"

Write-Host "  ✓ Schedule: $scheduleChoice (cron: $schedule)" -ForegroundColor Gray

# ---------------------------
# Summary
# ---------------------------
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Configuration Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "All settings saved to: $configFile" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Run 01-FinanceHub-Provisioner.ps1 (SharePoint & Entra App)" -ForegroundColor White
Write-Host "  2. Run 01b-EXO-SharedMailboxes.ps1 (Exchange Online)" -ForegroundColor White
Write-Host "  3. Run 02-Configure-Azure-Resources.ps1 (Azure provisioning)" -ForegroundColor White
Write-Host "  4. Run 03-Generate-Function-Code.ps1 (Function App code)" -ForegroundColor White
Write-Host "  5. Run 04-Generate-StaticWebApp-Code.ps1 (React app code)" -ForegroundColor White
Write-Host "  6. Run 05-Generate-LogicApp-Workflow.ps1 (Logic App workflow)" -ForegroundColor White
Write-Host ""

# Display summary
$ini = Get-IniContent $configFile
Write-Host "Configuration Summary:" -ForegroundColor Cyan
Write-Host "---------------------" -ForegroundColor Gray
Write-Host "Tenant:            $($ini.Tenant.ShortName).onmicrosoft.com" -ForegroundColor White
Write-Host "Company:           $($ini.Brand.BrandShort)" -ForegroundColor White
Write-Host "Currency:          $($ini.Finance.BaseCurrency)" -ForegroundColor White
Write-Host "Site:              $($ini.SharePoint.SiteUrl)" -ForegroundColor White
Write-Host "Quotes Email:      $($ini.Email.QuotesFromEmail)" -ForegroundColor White
Write-Host "Invoices Email:    $($ini.Email.InvoicesFromEmail)" -ForegroundColor White
Write-Host "Function App:      $($ini.Azure.FunctionAppName)" -ForegroundColor White
Write-Host ""
