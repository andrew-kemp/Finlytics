param([switch]$ForceRecreateApp)
$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

function LH_Get-IniContent {
    param([string]$Path)
    $ini = @{}
    if (-not (Test-Path $Path)) { return $ini }
    $section = ""
    switch -regex -file $Path {
        "^\s*\[(.+)\]\s*$" {
            $section = $matches[1]
            if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} }
        }
        "^\s*([^=]+?)\s*=\s*(.*)$" {
            if ([string]::IsNullOrWhiteSpace($section)) {
                $section = "Default"
                if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} }
            }
            $ini[$section][$matches[1].Trim()] = $matches[2]
        }
    }
    return $ini
}

function LH_Set-IniValue {
    param([string]$Path,[string]$Section,[string]$Key,[string]$Value)
    if (-not (Test-Path $Path)) { "# Accounting Hub Configuration" | Set-Content $Path -Encoding UTF8 }
    $content = Get-Content $Path -ErrorAction SilentlyContinue
    $inSection = $false; $found = $false; $sectionExists = $false; $newContent = @()
    $keyPattern = "^\s*" + [regex]::Escape($Key) + "\s*="
    foreach ($line in $content) {
        if ($line -match "^\[$Section\]") { $inSection = $true; $sectionExists = $true; $newContent += $line }
        elseif ($line -match "^\[.*\]") { if ($inSection -and -not $found) { $newContent += "$Key=$Value"; $found = $true }; $inSection = $false; $newContent += $line }
        elseif ($inSection -and $line -match $keyPattern) { $newContent += "$Key=$Value"; $found = $true }
        else { $newContent += $line }
    }
    if ($inSection -and -not $found) { $newContent += "$Key=$Value" }
    if (-not $sectionExists) { $newContent += ""; $newContent += "[$Section]"; $newContent += "$Key=$Value" }
    $newContent | Set-Content -Path $Path -Force -Encoding UTF8
}

function LH_Get-IniValueOrPrompt {
    param([string]$Path,[string]$Section,[string]$Key,[string]$Prompt,[string]$Default = "",[switch]$AllowBlank)
    $config = LH_Get-IniContent -Path $Path
    $value = $null
    if ($config.ContainsKey($Section)) { $value = $config[$Section][$Key] }
    if ([string]::IsNullOrWhiteSpace($value)) {
        if ([string]::IsNullOrWhiteSpace($Default)) { $value = Read-Host $Prompt }
        else { $input = Read-Host "$Prompt [$Default]"; $value = if ([string]::IsNullOrWhiteSpace($input)) { $Default } else { $input } }
        if (-not $AllowBlank) { while ([string]::IsNullOrWhiteSpace($value)) { $value = Read-Host $Prompt } }
        if (-not [string]::IsNullOrWhiteSpace($value)) { LH_Set-IniValue -Path $Path -Section $Section -Key $Key -Value $value }
    }
    return $value
}

function LH_Ensure-Module {
    param([string]$Name,[string]$MinVersion="0.0.0")
    $installed = Get-Module -ListAvailable -Name $Name | Where-Object { $_.Version -ge [version]$MinVersion } | Select-Object -First 1
    if (-not $installed) { Write-Host "Installing $Name..." -ForegroundColor Yellow; Install-Module -Name $Name -MinimumVersion $MinVersion -Scope CurrentUser -Force -AllowClobber }
    else { Write-Host "✓ $Name v$($installed.Version) installed" -ForegroundColor Green }
}

function LH_Ensure-List {
    param([string]$Title,[string]$Template = "GenericList",[bool]$EnableAttachments = $true,[string]$Url = $null,[switch]$OnQuickLaunch)
    $l = Get-PnPList -Identity $Title -ErrorAction SilentlyContinue
    if (-not $l) {
        Write-Host "Creating: $Title" -ForegroundColor Yellow
        if ([string]::IsNullOrWhiteSpace($Url)) { $Url = ($Title -replace '\s+', '-') }
        $params = @{ Title=$Title; Template=$Template; Url=$Url }
        if ($OnQuickLaunch) { $params["OnQuickLaunch"] = $true }
        New-PnPList @params | Out-Null
        if ($Template -eq "GenericList") { Set-PnPList -Identity $Title -EnableAttachments:$EnableAttachments | Out-Null }
    } else { Write-Host "✓ Exists: $Title" -ForegroundColor Green }
}

function LH_Ensure-Field {
    param(
        [Parameter(Mandatory)][string]$List,
        [Parameter(Mandatory)][string]$DisplayName,
        [Parameter(Mandatory)][string]$InternalName,
        [Parameter(Mandatory)][ValidateSet("Text","Note","Number","Currency","DateTime","Choice","Boolean","URL","Lookup")][string]$Type,
        [string[]]$Choices,
        [string]$LookupList,
        [string]$LookupField = "Title",
        [bool]$Required = $false,
        [bool]$AddToDefaultView = $true,
        [bool]$EnforceUnique = $false,
        [bool]$Indexed = $false,
        [hashtable]$FieldValues
    )
    $exists = Get-PnPField -List $List -Identity $InternalName -ErrorAction SilentlyContinue
    if (-not $exists) {
        if ($Type -eq "Choice" -and $Choices) { Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type Choice -Choices $Choices -AddToDefaultView:$AddToDefaultView | Out-Null }
        elseif ($Type -eq "Lookup") {
            if ([string]::IsNullOrWhiteSpace($LookupList)) { throw "LookupList required for $InternalName" }
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type Lookup -LookupList $LookupList -LookupField $LookupField -AddToDefaultView:$AddToDefaultView | Out-Null
        }
        else { Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type $Type -AddToDefaultView:$AddToDefaultView | Out-Null }
    }
    if ($Indexed -or $EnforceUnique) { Set-PnPField -List $List -Identity $InternalName -Values @{ Indexed = $true } | Out-Null }
    if ($EnforceUnique) { Set-PnPField -List $List -Identity $InternalName -Values @{ EnforceUniqueValues = $true } | Out-Null }
    if ($Required) { Set-PnPField -List $List -Identity $InternalName -Values @{ Required = $true } | Out-Null }
    if ($FieldValues) { Set-PnPField -List $List -Identity $InternalName -Values $FieldValues | Out-Null }
}

function LH_Ensure-View {
    param([string]$List,[string]$Title,[string[]]$Fields,[string]$Query,[int]$RowLimit=50)
    $v = Get-PnPView -List $List -Identity $Title -ErrorAction SilentlyContinue
    if (-not $v) { Add-PnPView -List $List -Title $Title -Fields $Fields -RowLimit $RowLimit -Query $Query | Out-Null; Write-Host "Created view: $Title" -ForegroundColor Yellow }
}

try {
    if (-not (Test-Path $configFile)) { "# Config" | Set-Content $configFile -Encoding UTF8 }
    LH_Ensure-Module -Name "PnP.PowerShell" -MinVersion "2.0.0"
    Import-Module PnP.PowerShell -ErrorAction Stop

    $tenantShort = LH_Get-IniValueOrPrompt -Path $configFile -Section "Tenant" -Key "ShortName" -Prompt "Tenant (e.g., kempy)"
    $tenantId = "$tenantShort.onmicrosoft.com"
    $rootUrl = "https://$tenantShort.sharepoint.com"
    $adminUrl = "https://$tenantShort-admin.sharepoint.com"
    LH_Set-IniValue -Path $configFile -Section "Tenant" -Key "TenantId" -Value $tenantId
    LH_Set-IniValue -Path $configFile -Section "Tenant" -Key "RootUrl" -Value $rootUrl
    LH_Set-IniValue -Path $configFile -Section "Tenant" -Key "AdminUrl" -Value $adminUrl

    $companyDisplay = LH_Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "CompanyDisplay" -Prompt "Company (e.g., AK)" -Default "AK"
    $product = LH_Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "Product" -Prompt "Product (LedgerHub)" -Default "LedgerHub"
    if ($product -notin @("LedgerHub","FinanceHub")) { $product = "LedgerHub" }

    $siteTitle = LH_Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteTitle" -Prompt "Site Title" -Default "$companyDisplay $product"
    $siteAlias = LH_Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteAlias" -Prompt "Site alias" -Default (($companyDisplay + $product) -replace '\s+','').ToLowerInvariant()
    $siteUrl = "$rootUrl/sites/$siteAlias"
    LH_Set-IniValue -Path $configFile -Section "SharePoint" -Key "SiteUrl" -Value $siteUrl

    $ownerUpn = LH_Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "OwnerUpn" -Prompt "Owner UPN" -AllowBlank

    $cfg = LH_Get-IniContent -Path $configFile
    $clientId = if ($cfg.ContainsKey("App")) { $cfg["App"]["ClientId"] } else { $null }
    $pfxPath = if ($cfg.ContainsKey("App")) { $cfg["App"]["PfxPath"] } else { $null }
    if ([string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($pfxPath) -or -not (Test-Path $pfxPath)) { throw "Missing ClientId/PFX in INI" }

    $certPwd = Read-Host -AsSecureString "PFX password"

    Write-Host "`nConnecting to admin..." -ForegroundColor Yellow
    Connect-PnPOnline -Url $adminUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $certPwd -ErrorAction Stop
    Write-Host "✓ Connected" -ForegroundColor Green

    $existingSite = Get-PnPTenantSite -Url $siteUrl -ErrorAction SilentlyContinue
    if (-not $existingSite) {
        if ([string]::IsNullOrWhiteSpace($ownerUpn)) { throw "OwnerUpn required" }
        Write-Host "Creating site..." -ForegroundColor Yellow
        New-PnPSite -Type TeamSite -Alias $siteAlias -Title $siteTitle -Owners $ownerUpn -IsPublic:$false | Out-Null
        Start-Sleep -Seconds 60
    } else { Write-Host "✓ Site exists" -ForegroundColor Green }

    Disconnect-PnPOnline

    Write-Host "`nConnecting to site..." -ForegroundColor Yellow
    Connect-PnPOnline -Url $siteUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $certPwd -ErrorAction Stop
    Write-Host "✓ Connected" -ForegroundColor Green

    LH_Ensure-List -Title "Customers" -Template "GenericList" -EnableAttachments:$false -Url "Customers" -OnQuickLaunch
    LH_Ensure-List -Title "Suppliers" -Template "GenericList" -EnableAttachments:$false -Url "Suppliers" -OnQuickLaunch
    LH_Ensure-List -Title "Ledger" -Template "GenericList" -EnableAttachments:$false -Url "Ledger" -OnQuickLaunch
    LH_Ensure-List -Title "Invoices" -Template "DocumentLibrary" -Url "Invoices" -OnQuickLaunch
    LH_Ensure-List -Title "Receipts" -Template "DocumentLibrary" -Url "Receipts" -OnQuickLaunch
    LH_Ensure-List -Title "InvoiceRequests" -Template "GenericList" -EnableAttachments:$false -Url "InvoiceRequests" -OnQuickLaunch

    Set-PnPField -List "Customers" -Identity "Title" -Values @{ Title = "Customer Name"; Required = $true } | Out-Null
    LH_Ensure-Field -List "Customers" -DisplayName "Customer Code" -InternalName "CustomerCode" -Type Text -Required:$true -Indexed:$true -EnforceUnique:$true
    LH_Ensure-Field -List "Customers" -DisplayName "Billing Email" -InternalName "BillingEmail" -Type Text
    LH_Ensure-Field -List "Customers" -DisplayName "Billing Address" -InternalName "BillingAddress" -Type Note
    LH_Ensure-Field -List "Customers" -DisplayName "Default Day Rate" -InternalName "DefaultDayRate" -Type Currency
    LH_Ensure-View -List "Customers" -Title "All Customers" -Fields @("Title","CustomerCode","BillingEmail","DefaultDayRate")

    Set-PnPField -List "Suppliers" -Identity "Title" -Values @{ Title = "Supplier Name"; Required = $true } | Out-Null
    LH_Ensure-View -List "Suppliers" -Title "All Suppliers" -Fields @("Title")

    Set-PnPField -List "Ledger" -Identity "Title" -Values @{ Title = "Reference"; Required = $true } | Out-Null
    LH_Ensure-Field -List "Ledger" -DisplayName "Type" -InternalName "EntryType" -Type Choice -Choices @("Income","Expense") -Required:$true
    LH_Ensure-Field -List "Ledger" -DisplayName "Date" -InternalName "EntryDate" -Type DateTime -Required:$true
    LH_Ensure-Field -List "Ledger" -DisplayName "Customer" -InternalName "LedgerCustomer" -Type Lookup -LookupList "Customers" -LookupField "Title" -AddToDefaultView:$true
    LH_Ensure-Field -List "Ledger" -DisplayName "Supplier" -InternalName "LedgerSupplier" -Type Lookup -LookupList "Suppliers" -LookupField "Title" -AddToDefaultView:$true
    LH_Ensure-Field -List "Ledger" -DisplayName "Category" -InternalName "Category" -Type Choice -Choices @("Sales","Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    LH_Ensure-Field -List "Ledger" -DisplayName "Related Document" -InternalName "RelatedDocument" -Type URL
    LH_Ensure-Field -List "Ledger" -DisplayName "Notes" -InternalName "Notes" -Type Note
    LH_Ensure-Field -List "Ledger" -DisplayName "Invoice Number" -InternalName "InvoiceNumber" -Type Text -Indexed:$true -EnforceUnique:$true
    LH_Ensure-Field -List "Ledger" -DisplayName "Invoice Date" -InternalName "InvoiceDate" -Type DateTime
    LH_Ensure-Field -List "Ledger" -DisplayName "Week Start" -InternalName "WeekStart" -Type DateTime
    LH_Ensure-Field -List "Ledger" -DisplayName "Week End" -InternalName "WeekEnd" -Type DateTime
    LH_Ensure-Field -List "Ledger" -DisplayName "Days Billed" -InternalName "DaysBilled" -Type Number
    LH_Ensure-Field -List "Ledger" -DisplayName "Day Rate" -InternalName "DayRate" -Type Currency
    LH_Ensure-Field -List "Ledger" -DisplayName "VAT Included" -InternalName "VATIncluded" -Type Boolean
    LH_Ensure-Field -List "Ledger" -DisplayName "VAT Rate (%)" -InternalName "VATRate" -Type Number -FieldValues @{ DefaultValue = "20" }
    LH_Ensure-Field -List "Ledger" -DisplayName "Amount Entered" -InternalName "AmountEntered" -Type Currency
    LH_Ensure-Field -List "Ledger" -DisplayName "Amount (net)" -InternalName "AmountNet" -Type Currency
    LH_Ensure-Field -List "Ledger" -DisplayName "VAT Amount" -InternalName "VATAmount" -Type Currency
    LH_Ensure-Field -List "Ledger" -DisplayName "Amount (gross)" -InternalName "AmountGross" -Type Currency
    LH_Ensure-Field -List "Ledger" -DisplayName "Paid" -InternalName "Paid" -Type Boolean
    LH_Ensure-Field -List "Ledger" -DisplayName "Paid Date" -InternalName "PaidDate" -Type DateTime

    $incQ = "<Where><Eq><FieldRef Name=""EntryType""/><Value Type=""Choice"">Income</Value></Eq></Where>"
    $expQ = "<Where><Eq><FieldRef Name=""EntryType""/><Value Type=""Choice"">Expense</Value></Eq></Where>"
    LH_Ensure-View -List "Ledger" -Title "Income" -Fields @("Title","InvoiceDate","InvoiceNumber","LedgerCustomer","DaysBilled","DayRate","AmountNet","VATRate","VATAmount","AmountGross","Paid","RelatedDocument") -Query $incQ
    LH_Ensure-View -List "Ledger" -Title "Expenses" -Fields @("Title","EntryDate","LedgerSupplier","Category","AmountNet","VATRate","VATAmount","AmountGross","Paid","RelatedDocument") -Query $expQ

    Set-PnPField -List "InvoiceRequests" -Identity "Title" -Values @{ Title = "Invoice Number"; Required = $true } | Out-Null
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Customer" -InternalName "IRCustomer" -Type Lookup -LookupList "Customers" -LookupField "Title" -Required:$true
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Week Start" -InternalName "IRWeekStart" -Type DateTime -Required:$true
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Week End" -InternalName "IRWeekEnd" -Type DateTime
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Days" -InternalName "IRDays" -Type Number
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Day Rate" -InternalName "IRDayRate" -Type Currency
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "VAT Rate (%)" -InternalName "IRVATRate" -Type Number -FieldValues @{ DefaultValue = "20" }
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Invoice Date" -InternalName "IRInvoiceDate" -Type DateTime
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Invoice Number (system)" -InternalName "IRInvoiceNumber" -Type Text -Indexed:$true -EnforceUnique:$true
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Status" -InternalName "IRStatus" -Type Choice -Choices @("Draft","Generated","Sent","Paid","Cancelled")
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "PDF Link" -InternalName "IRPdfLink" -Type URL
    LH_Ensure-Field -List "InvoiceRequests" -DisplayName "Notes" -InternalName "IRNotes" -Type Note
    LH_Ensure-View -List "InvoiceRequests" -Title "All Requests" -Fields @("Title","IRCustomer","IRWeekStart","IRWeekEnd","IRDays","IRDayRate","IRVATRate","IRInvoiceDate","IRStatus","IRPdfLink")

    Write-Host "`n✓ Complete" -ForegroundColor Green
    Write-Host "Site: $siteUrl" -ForegroundColor Gray
    Disconnect-PnPOnline | Out-Null
} catch {
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    try { Disconnect-PnPOnline | Out-Null } catch {}
    exit 1
}
