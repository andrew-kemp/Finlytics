<#
    FinanceHubProvisioner.ps1
    Version-safe, idempotent provisioning of a Finance / Ledger Hub in SharePoint Online.

    Includes:
      - Entra App + cert auth for PnP (no PFX password stored in INI)
      - Team site creation / reuse
      - Regional settings (Locale, Time zone, first day, 24h clock) with CSOM fallback
      - Base currency prompt → CurrencyLocaleId applied to all Currency fields
      - Lists: Customers, Suppliers, Ledger; Libraries: Invoices, Receipts
      - VAT fields (VATApplicability, VATIncluded default Yes, VATRate default 20)
      - Customer PO/Ref, Paid/DatePaid, DLA, RelatedDocument, Notes
      - Customers commercial fields: Billing Email, Billing Address, Default Day Rate
      - Customers "All Customers" view (optionally set as default)
      - Company Settings list (seeded “Default” profile) for identity & FY boundary
      - Ledger NeedsCalc (default Yes) to drive Flow calculations
      - Optional invoice discounts fields and view
      - Views: Ledger Income / Expenses / Unpaid Invoices
      - Lookup creation via Add-PnPFieldFromXml (works on PnP v3.x incl. 3.1.0)

    Persists config: finance-hub-config.ini
#>

param(
    [switch]$ForceRecreateApp
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

# ===========================
# INI HELPERS (defensive)
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
    if (-not $ini.ContainsKey($Section)) { $ini[$Section] = @{} }
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
    if (-not (Test-Path $Path)) { "# FinanceHub Configuration" | Set-Content -Path $Path -Encoding UTF8 }
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

# ===========================
# UTILITY HELPERS
# ===========================
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

function Ensure-List {
    param([string]$Title,[string]$Template="GenericList",[bool]$EnableAttachments=$true,[string]$Url=$null,[switch]$OnQuickLaunch)
    $l = Get-PnPList -Identity $Title -ErrorAction SilentlyContinue
    if (-not $l) {
        Write-Host "Creating list/library: $Title" -ForegroundColor Yellow
        if ([string]::IsNullOrWhiteSpace($Url)) { $Url = ($Title -replace '\s+','-') }
        $params = @{ Title=$Title; Template=$Template; Url=$Url }
        if ($OnQuickLaunch) { $params["OnQuickLaunch"] = $true }
        New-PnPList @params | Out-Null
        if ($Template -eq "GenericList") { Set-PnPList -Identity $Title -EnableAttachments:$EnableAttachments | Out-Null }
    } else { Write-Host "✓ Exists: $Title" -ForegroundColor Green }
}

# --- Version-safe lookup creation using Add-PnPFieldFromXml ---
function Ensure-LookupField {
    param(
        [string]$ListTitle,[string]$DisplayName,[string]$InternalName,
        [string]$LookupListTitle,[string]$LookupField = "Title",
        [bool]$Required = $false,[bool]$AddToDefaultView = $true
    )
    $existing = Get-PnPField -List $ListTitle -Identity $InternalName -ErrorAction SilentlyContinue
    if ($existing) { return }

    $target = Get-PnPList -Identity $LookupListTitle -ErrorAction Stop
    $listGuid = $target.Id.Guid
    $req = if ($Required) { "TRUE" } else { "FALSE" }

    $xml = @"
<Field Type="Lookup"
       DisplayName="$DisplayName"
       StaticName="$InternalName"
       Name="$InternalName"
       List="{$listGuid}"
       ShowField="$LookupField"
       Required="$req" />
"@

    Add-PnPFieldFromXml -List $ListTitle -FieldXml $xml | Out-Null

    if ($AddToDefaultView) {
        $view = Get-PnPView -List $ListTitle -Identity "All Items" -ErrorAction SilentlyContinue
        if ($view) {
            $fields = @($view.ViewFields)
            if ($fields -notcontains $InternalName) {
                $fields += $InternalName
                Set-PnPView -List $ListTitle -Identity $view.Id -Fields $fields | Out-Null
            }
        }
    }
}

function Ensure-Field {
    param(
        [string]$List,[string]$DisplayName,[string]$InternalName,
        [ValidateSet("Text","Note","Number","Currency","DateTime","Choice","Boolean","URL","Lookup")]$Type,
        [string[]]$Choices = $null,
        [string]$LookupList = $null,
        [string]$LookupField = "Title",
        [bool]$Required=$false,
        [bool]$AddToDefaultView=$true,
        [bool]$EnforceUnique=$false,
        [bool]$Indexed=$false
    )
    $exists = Get-PnPField -List $List -Identity $InternalName -ErrorAction SilentlyContinue
    if (-not $exists) {
        if ($Type -eq "Choice" -and $Choices) {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type Choice -Choices $Choices -AddToDefaultView:$AddToDefaultView | Out-Null
        } elseif ($Type -eq "Lookup" -and $LookupList) {
            Ensure-LookupField -ListTitle $List -DisplayName $DisplayName -InternalName $InternalName -LookupListTitle $LookupList -LookupField $LookupField -Required:$Required -AddToDefaultView:$AddToDefaultView
        } else {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type $Type -AddToDefaultView:$AddToDefaultView | Out-Null
        }
    }
    if ($Indexed -or $EnforceUnique) { Set-PnPField -List $List -Identity $InternalName -Values @{ Indexed = $true } | Out-Null }
    if ($EnforceUnique) { Set-PnPField -List $List -Identity $InternalName -Values @{ EnforceUniqueValues = $true } | Out-Null }
    if ($Required) { Set-PnPField -List $List -Identity $InternalName -Values @{ Required = $true } | Out-Null }
}

function Ensure-View {
    param([string]$List,[string]$Title,[string[]]$Fields,[string]$Query=$null,[int]$RowLimit=50)
    $v = Get-PnPView -List $List -Identity $Title -ErrorAction SilentlyContinue
    if (-not $v) {
        Add-PnPView -List $List -Title $Title -Fields $Fields -RowLimit $RowLimit -Query $Query | Out-Null
        Write-Host "Created view: $Title (List: $List)" -ForegroundColor Yellow
    }
}

# ---- Currency helpers ----
function Get-CurrencyLocaleId {
    param([string]$Code)
    $map = @{
        "GBP" = 2057   # en-GB → £
        "USD" = 1033   # en-US → $
        "EUR" = 6153   # en-IE → €
        "AUD" = 3081   # en-AU → A$
        "CAD" = 4105   # en-CA → C$
        "NZD" = 5129   # en-NZ → NZ$
        "ZAR" = 7177   # en-ZA → R
        "SEK" = 1053   # sv-SE → kr
        "NOK" = 1044   # nb-NO → kr
        "DKK" = 1030   # da-DK → kr
        "CHF" = 2055   # de-CH → CHF
        "JPY" = 1041   # ja-JP → ¥
    }
    $code = $Code.ToUpperInvariant()
    if ($map.ContainsKey($code)) { return $map[$code] }
    return 2057  # default to en-GB if unknown
}

function Set-CurrencyOnField {
    param([string]$List,[string]$FieldInternalName,[int]$CurrencyLcid)
    try {
        Set-PnPField -List $List -Identity $FieldInternalName -Values @{ CurrencyLocaleId = $CurrencyLcid } | Out-Null
    } catch {
        Write-Warning ("Could not set currency on {0}/{1}: {2}" -f $List, $FieldInternalName, $_.Exception.Message)
    }
}

# ---- Regional settings fallback (if Set-PnPRegionalSettings not available) ----
function Convert-FirstDayToEnum {
    param([string]$FirstDay)
    switch ($FirstDay.ToLower()) {
        "monday"    { return 1 }
        "tuesday"   { return 2 }
        "wednesday" { return 3 }
        "thursday"  { return 4 }
        "friday"    { return 5 }
        "saturday"  { return 6 }
        default     { return 0 } # Sunday
    }
}

function Apply-RegionalSettings {
    param(
        [int]$LocaleId,
        [string]$TimeZoneName = "GMT Standard Time",
        [string]$FirstDayOfWeek = "Monday",
        [bool]$Use24HourClock = $true
    )

    $cmd = Get-Command -Name Set-PnPRegionalSettings -ErrorAction SilentlyContinue
    if ($cmd) {
        try {
            $zones = Get-PnPTimeZoneId
            $tz = $zones | Where-Object { $_.Description -eq $TimeZoneName -or $_.Id -eq $TimeZoneName } | Select-Object -First 1
            if (-not $tz) {
                $tz = $zones | Where-Object { $_.Description -eq "GMT Standard Time" } | Select-Object -First 1
                Write-Warning "Time zone '$TimeZoneName' not found, falling back to 'GMT Standard Time'."
            }
            Set-PnPRegionalSettings -LocaleId $LocaleId -TimeZone $tz.Id -FirstDayOfWeek $FirstDayOfWeek -Time24 $Use24HourClock | Out-Null
            Write-Host "✓ Regional settings applied via Set-PnPRegionalSettings" -ForegroundColor Green
            return
        } catch {
            Write-Warning ("Set-PnPRegionalSettings failed, using CSOM fallback: {0}" -f $_.Exception.Message)
        }
    }

    # CSOM fallback
    try {
        $ctx = Get-PnPContext
        $web = Get-PnPWeb
        $ctx.Load($web.RegionalSettings)
        $ctx.ExecuteQuery()

        $zones = Get-PnPTimeZoneId
        $tzObj = $zones | Where-Object { $_.Description -eq $TimeZoneName -or $_.Id -eq $TimeZoneName } | Select-Object -First 1
        if (-not $tzObj) {
            $tzObj = $zones | Where-Object { $_.Description -eq "GMT Standard Time" } | Select-Object -First 1
            Write-Warning "Time zone '$TimeZoneName' not found, falling back to 'GMT Standard Time'."
        }

        $tz = $web.RegionalSettings.TimeZones.GetById([int]$tzObj.Id)
        $ctx.Load($tz)
        $ctx.ExecuteQuery()

        $web.RegionalSettings.LocaleId = [int]$LocaleId
        $web.RegionalSettings.TimeZone = $tz
        $web.RegionalSettings.FirstDayOfWeek = Convert-FirstDayToEnum -FirstDay $FirstDayOfWeek
        $web.RegionalSettings.Time24 = [bool]$Use24HourClock
        $web.Update()
        $ctx.ExecuteQuery()

        Write-Host "✓ Regional settings applied via CSOM fallback" -ForegroundColor Green
    } catch {
        Write-Warning ("Could not set regional settings: {0}" -f $_.Exception.Message)
    }
}

# ===========================
# MAIN
# ===========================
try {
    if (-not (Test-Path $configFile)) { "# FinanceHub Configuration" | Set-Content $configFile -Encoding UTF8 }

    Ensure-Module -Name "PnP.PowerShell" -MinVersion "2.0.0"
    Import-Module PnP.PowerShell -ErrorAction Stop

    # Tenant & branding
    $tenantShort = Get-IniValueOrPrompt -Path $configFile -Section "Tenant" -Key "ShortName" -Prompt "Tenant short name (e.g. kempy)"
    $tenantId = "$tenantShort.onmicrosoft.com"
    $rootUrl  = "https://$tenantShort.sharepoint.com"
    $adminUrl = "https://$tenantShort-admin.sharepoint.com"
    Set-IniValue -Path $configFile -Section "Tenant" -Key "TenantId" -Value $tenantId
    Set-IniValue -Path $configFile -Section "Tenant" -Key "RootUrl" -Value $rootUrl
    Set-IniValue -Path $configFile -Section "Tenant" -Key "AdminUrl" -Value $adminUrl

    $company = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "CompanyDisplay" -Prompt "Company initials (e.g. AK)" -Default "AK"
    $product = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "Product" -Prompt "Hub type (LedgerHub or FinanceHub)" -Default "LedgerHub"
    if ($product -notin @("LedgerHub","FinanceHub")) { $product = "LedgerHub"; Set-IniValue -Path $configFile -Section "Brand" -Key "Product" -Value $product }

    $siteTitleDefault = "$company $product"
    $siteTitle = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteTitle" -Prompt "SharePoint site title" -Default $siteTitleDefault
    $aliasDefault = (($company + $product) -replace '\s+','').ToLowerInvariant()
    $siteAlias = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteAlias" -Prompt "Site URL name, /sites/<name>" -Default $aliasDefault
    $siteUrl = "$rootUrl/sites/$siteAlias"
    Set-IniValue -Path $configFile -Section "SharePoint" -Key "SiteUrl" -Value $siteUrl

    $ownerUpn = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "OwnerUpn" -Prompt "Site Owner UPN (needed if the site must be created)" -AllowBlank

    # Regional settings (UK defaults; configurable)
    $localeIdStr = Get-IniValueOrPrompt -Path $configFile -Section "Regional" -Key "LocaleId" -Prompt "LocaleId (2057=en-GB, 1033=en-US, etc.)" -Default "2057"
    [int]$localeId = $localeIdStr
    Set-IniValue -Path $configFile -Section "Regional" -Key "LocaleId" -Value "$localeId"

    $timeZoneName = Get-IniValueOrPrompt -Path $configFile -Section "Regional" -Key "TimeZone" -Prompt "Time zone display name" -Default "GMT Standard Time"
    Set-IniValue -Path $configFile -Section "Regional" -Key "TimeZone" -Value $timeZoneName

    $firstDay = Get-IniValueOrPrompt -Path $configFile -Section "Regional" -Key "FirstDayOfWeek" -Prompt "First day of week (Sunday/Monday/...)" -Default "Monday"
    $use24h = Get-IniValueOrPrompt -Path $configFile -Section "Regional" -Key "Use24HourClock" -Prompt "Use 24-hour clock? (Y/N)" -Default "Y"
    $use24hBool = ($use24h.ToUpperInvariant() -eq "Y")

    # Base currency prompt
    $baseCurrencyCode = Get-IniValueOrPrompt -Path $configFile -Section "Finance" -Key "BaseCurrency" -Prompt "Base currency code (GBP, USD, EUR, ...)" -Default "GBP"
    $currencyLcid = Get-CurrencyLocaleId -Code $baseCurrencyCode
    Set-IniValue -Path $configFile -Section "Finance" -Key "CurrencyLCID" -Value "$currencyLcid"

    # Discounts toggle
    $enableDiscounts = Get-IniValueOrPrompt -Path $configFile -Section "Invoices" -Key "EnableDiscounts" -Prompt "Enable invoice discounts? (Y/N)" -Default "Y"
    $enableDiscounts = if ($enableDiscounts.ToUpperInvariant() -eq "Y") { $true } else { $false }

    # App registration
    $appNameDefault = "$company-$product-PnP"
    $appName = Get-IniValueOrPrompt -Path $configFile -Section "App" -Key "AppName" -Prompt "App registration display name" -Default $appNameDefault
    $certFolder = Join-Path $scriptRoot "cert-output"; if (-not (Test-Path $certFolder)) { New-Item -ItemType Directory -Path $certFolder | Out-Null }
    $cfg = Get-IniContent -Path $configFile
    $clientId = $cfg["App"]["ClientId"]; $pfxPath = $cfg["App"]["PfxPath"]; $thumb = $cfg["App"]["Thumbprint"]
    $certPassword = $null

    if ($ForceRecreateApp -or -not $clientId -or -not $pfxPath -or -not (Test-Path $pfxPath)) {
        do {
            $pw1 = Read-Host -AsSecureString "Enter certificate password"
            $pw2 = Read-Host -AsSecureString "Confirm certificate password"
            $b1=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw1); $b2=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw2)
            $s1=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($b1); $s2=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($b2)
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b1); [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b2)
        } while (-not $s1 -or $s1 -ne $s2)
        $certPassword = $pw1

        $graphPerms=@("Group.ReadWrite.All","User.Read.All","Sites.ReadWrite.All")
        $spoPerms=@("Sites.FullControl.All")

        $reg = Register-PnPAzureADApp -ApplicationName $appName -Tenant $tenantId -Store CurrentUser -OutPath $certFolder -GraphApplicationPermissions $graphPerms -SharePointApplicationPermissions $spoPerms
        $clientId = $reg.'AzureAppId/ClientId'; if (-not $clientId) { $clientId = $reg.AzureAppId }
        Start-Sleep 45

        $storeCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*$appName*" -and $_.HasPrivateKey } | Sort-Object NotAfter -Descending | Select-Object -First 1
        if (-not $storeCert) { throw "Certificate not found for $appName" }

        $pfxPath = Join-Path $certFolder "$appName.pfx"
        Export-PfxCertificate -Cert $storeCert -FilePath $pfxPath -Password $certPassword -Force
        $thumb = $storeCert.Thumbprint

        Set-IniValue -Path $configFile -Section "App" -Key "ClientId" -Value $clientId
        Set-IniValue -Path $configFile -Section "App" -Key "PfxPath" -Value $pfxPath
        Set-IniValue -Path $configFile -Section "App" -Key "Thumbprint" -Value $thumb

        $consent = "https://login.microsoftonline.com/$tenantId/adminconsent?client_id=$clientId"
        Write-Host "Grant admin consent: $consent" -ForegroundColor Yellow
        try { Start-Process $consent | Out-Null } catch {}
        Read-Host "Press Enter after consent"
        Start-Sleep 45
    } else {
        $certPassword = Read-Host -AsSecureString "Enter the PFX certificate password"
    }

    # Connect Admin & ensure site
    Connect-PnPOnline -Url $adminUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $certPassword
    $existingSite = Get-PnPTenantSite -Url $siteUrl -ErrorAction SilentlyContinue
    if (-not $existingSite) {
        if ([string]::IsNullOrWhiteSpace($ownerUpn)) {
            $ownerUpn = Read-Host "Owner UPN required to create site"
            while ([string]::IsNullOrWhiteSpace($ownerUpn)) { $ownerUpn = Read-Host "Owner UPN cannot be blank. Enter Site Owner UPN" }
            Set-IniValue -Path $configFile -Section "SharePoint" -Key "OwnerUpn" -Value $ownerUpn
        }
        New-PnPSite -Type TeamSite -Alias $siteAlias -Title $siteTitle -Owners $ownerUpn -IsPublic:$false | Out-Null
        for ($i=0;$i -lt 12;$i++){ Start-Sleep 10; $existingSite = Get-PnPTenantSite -Url $siteUrl -ErrorAction SilentlyContinue; if ($existingSite){break} }
        if (-not $existingSite) { throw "Site did not appear ready in time: $siteUrl" }
    } else { Write-Host "✓ Site already exists" -ForegroundColor Green }
    Disconnect-PnPOnline

    # Connect Site
    Connect-PnPOnline -Url $siteUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $certPassword
    Write-Host "✓ Connected to site" -ForegroundColor Green

    # Apply regional settings (native or CSOM fallback)
    Apply-RegionalSettings -LocaleId $localeId -TimeZoneName $timeZoneName -FirstDayOfWeek $firstDay -Use24HourClock $use24hBool

    # Create lists & libraries
    Ensure-List -Title "Customers" -Template "GenericList" -EnableAttachments:$false -Url "Customers" -OnQuickLaunch
    Ensure-List -Title "Suppliers" -Template "GenericList" -EnableAttachments:$false -Url "Suppliers" -OnQuickLaunch
    Ensure-List -Title "Ledger"    -Template "GenericList" -EnableAttachments:$false -Url "Ledger" -OnQuickLaunch
    Ensure-List -Title "Invoices"  -Template "DocumentLibrary" -Url "Invoices" -OnQuickLaunch
    Ensure-List -Title "Receipts"  -Template "DocumentLibrary" -Url "Receipts" -OnQuickLaunch

    # =======================
    # Customers (with commercial fields + view)
    # =======================
    Ensure-Field -List "Customers" -DisplayName "Customer Name"    -InternalName "Title"           -Type Text -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Customers" -DisplayName "Customer Code"    -InternalName "CustomerCode"    -Type Text -Required:$true -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Customers" -DisplayName "Contact Name"     -InternalName "ContactName"     -Type Text
    Ensure-Field -List "Customers" -DisplayName "Email"            -InternalName "Email"           -Type Text
    Ensure-Field -List "Customers" -DisplayName "Address"          -InternalName "Address"         -Type Note

    # NEW: Billing + Day Rate
    Ensure-Field -List "Customers" -DisplayName "Billing Email"    -InternalName "BillingEmail"    -Type Text
    Ensure-Field -List "Customers" -DisplayName "Billing Address"  -InternalName "BillingAddress"  -Type Note
    Ensure-Field -List "Customers" -DisplayName "Default Day Rate" -InternalName "DefaultDayRate"  -Type Currency
    Set-CurrencyOnField -List "Customers" -FieldInternalName "DefaultDayRate" -CurrencyLcid $currencyLcid

    # NEW: Customers view
    Ensure-View -List "Customers" -Title "All Customers" -Fields @(
        "Title","CustomerCode","BillingEmail","BillingAddress","DefaultDayRate","ContactName","Email","Address"
    )
    # (Optional) make it default:
    try {
        $v = Get-PnPView -List "Customers" -Identity "All Customers" -ErrorAction Stop
        Set-PnPView -List "Customers" -Identity $v.Id -Values @{ DefaultView = $true } | Out-Null
    } catch {
        Write-Warning ("Could not set default view on Customers: {0}" -f $_.Exception.Message)
    }

    # =======================
    # Suppliers
    # =======================
    Ensure-Field -List "Suppliers" -DisplayName "Supplier Name"    -InternalName "Title"            -Type Text -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Suppliers" -DisplayName "Supplier Code"    -InternalName "SupplierCode"     -Type Text -Required:$true -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Suppliers" -DisplayName "Category"         -InternalName "SupplierCategory" -Type Choice -Choices @("Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Suppliers" -DisplayName "Default VAT Rate" -InternalName "DefaultVATRate"   -Type Number
    Ensure-Field -List "Suppliers" -DisplayName "Email"            -InternalName "SupplierEmail"    -Type Text

    # =======================
    # Ledger
    # =======================
    Ensure-Field -List "Ledger" -DisplayName "Invoice or Expense Number" -InternalName "Title" -Type Text
    Ensure-Field -List "Ledger" -DisplayName "Entry Type" -InternalName "EntryType" -Type Choice -Choices @("Income","Expense") -Required:$true
    Ensure-Field -List "Ledger" -DisplayName "Entry Date" -InternalName "EntryDate" -Type DateTime -Required:$true
    Ensure-Field -List "Ledger" -DisplayName "Customer"   -InternalName "Customer"  -Type Lookup -LookupList "Customers" -LookupField "Title"
    Ensure-Field -List "Ledger" -DisplayName "Supplier"   -InternalName "Supplier"  -Type Lookup -LookupList "Suppliers" -LookupField "Title"
    Ensure-Field -List "Ledger" -DisplayName "Supplier Free Text" -InternalName "SupplierFreeText" -Type Text
    Ensure-Field -List "Ledger" -DisplayName "Customer PO or Ref" -InternalName "CustomerPORef"   -Type Text
    Ensure-Field -List "Ledger" -DisplayName "Category"           -InternalName "Category"        -Type Choice -Choices @("Sales","Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")

    # VAT controls
    Ensure-Field -List "Ledger" -DisplayName "VAT Applicability" -InternalName "VATApplicability" -Type Choice -Choices @("Standard","Reduced","Zero-rated","Exempt","Outside Scope")
    Ensure-Field -List "Ledger" -DisplayName "VAT Included"      -InternalName "VATIncluded"      -Type Boolean
    Set-PnPField -List "Ledger" -Identity "VATIncluded" -Values @{ DefaultValue = "1" } | Out-Null
    Ensure-Field -List "Ledger" -DisplayName "VAT Rate"          -InternalName "VATRate"          -Type Number
    Set-PnPField -List "Ledger" -Identity "VATRate" -Values @{ DefaultValue = "20" } | Out-Null

    Ensure-Field -List "Ledger" -DisplayName "Amount Net"   -InternalName "AmountNet"   -Type Currency
    Ensure-Field -List "Ledger" -DisplayName "VAT Amount"   -InternalName "VATAmount"   -Type Currency
    Ensure-Field -List "Ledger" -DisplayName "Amount Gross" -InternalName "AmountGross" -Type Currency

    Ensure-Field -List "Ledger" -DisplayName "Payment Method"    -InternalName "PaymentMethod"    -Type Choice -Choices @("Bank","Card","Cash","DLA","Other")
    Ensure-Field -List "Ledger" -DisplayName "Paid"              -InternalName "Paid"             -Type Boolean
    Ensure-Field -List "Ledger" -DisplayName "Date Paid"         -InternalName "DatePaid"         -Type DateTime
    Ensure-Field -List "Ledger" -DisplayName "Payment Reference" -InternalName "PaymentReference" -Type Text

    Ensure-Field -List "Ledger" -DisplayName "Tax Year"       -InternalName "TaxYear"       -Type Text
    Ensure-Field -List "Ledger" -DisplayName "Financial Year" -InternalName "FinancialYear" -Type Text
    Ensure-Field -List "Ledger" -DisplayName "DLA"            -InternalName "IsDLA"         -Type Boolean
    Ensure-Field -List "Ledger" -DisplayName "Related Document" -InternalName "RelatedDocument" -Type URL
    Ensure-Field -List "Ledger" -DisplayName "Notes"          -InternalName "Notes"         -Type Note

    # NEW: NeedsCalc (default Yes) so direct list edits get calculated by Flow
    Ensure-Field -List "Ledger" -DisplayName "Needs Calc" -InternalName "NeedsCalc" -Type Boolean
    Set-PnPField -List "Ledger" -Identity "NeedsCalc" -Values @{ DefaultValue = "1" } | Out-Null

    # Views
    Ensure-View -List "Ledger" -Title "Income" -Fields @("Title","EntryDate","Customer","CustomerPORef","VATApplicability","AmountNet","VATAmount","AmountGross","Paid","RelatedDocument","TaxYear","FinancialYear") -Query '<Where><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Income</Value></Eq></Where>'
    Ensure-View -List "Ledger" -Title "Expenses" -Fields @("Title","EntryDate","Supplier","VATApplicability","Category","AmountNet","VATAmount","AmountGross","Paid","RelatedDocument","TaxYear","FinancialYear") -Query '<Where><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Expense</Value></Eq></Where>'
    Ensure-View -List "Ledger" -Title "Unpaid Invoices" -Fields @("Title","EntryDate","Customer","AmountGross","Paid","DatePaid","RelatedDocument") -Query '<Where><And><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Income</Value></Eq><Eq><FieldRef Name="Paid"/><Value Type="Integer">0</Value></Eq></And></Where>'

    # =======================
    # Invoices
    # =======================
    Ensure-Field -List "Invoices" -DisplayName "Invoice Number" -InternalName "InvoiceNumber" -Type Text -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Invoices" -DisplayName "Customer"       -InternalName "InvCustomer"   -Type Lookup -LookupList "Customers" -LookupField "Title"
    Ensure-Field -List "Invoices" -DisplayName "PO Reference"   -InternalName "POReference"   -Type Text
    Ensure-Field -List "Invoices" -DisplayName "Date Issued"    -InternalName "InvDateIssued" -Type DateTime
    Ensure-Field -List "Invoices" -DisplayName "Date Paid"      -InternalName "InvDatePaid"   -Type DateTime
    Ensure-Field -List "Invoices" -DisplayName "Status"         -InternalName "InvStatus"     -Type Choice -Choices @("Draft","Issued","Paid","Overdue")
    Ensure-Field -List "Invoices" -DisplayName "Amount Net"     -InternalName "InvAmountNet"  -Type Currency
    Ensure-Field -List "Invoices" -DisplayName "VAT Amount"     -InternalName "InvVATAmount"  -Type Currency
    Ensure-Field -List "Invoices" -DisplayName "Amount Gross"   -InternalName "InvAmountGross" -Type Currency
    Ensure-Field -List "Invoices" -DisplayName "Tax Year"       -InternalName "TaxYear"       -Type Text
    Ensure-Field -List "Invoices" -DisplayName "Financial Year" -InternalName "FinancialYear" -Type Text

    Ensure-View -List "Invoices" -Title "All Invoices" -Fields @("FileLeafRef","InvoiceNumber","InvCustomer","POReference","InvDateIssued","InvStatus","InvAmountNet","InvVATAmount","InvAmountGross","TaxYear","FinancialYear")

    # Optional discounts for invoices
    if ($enableDiscounts) {
        Ensure-Field -List "Invoices" -DisplayName "Discount Type"   -InternalName "InvDiscountType"   -Type Choice -Choices @("None","Percentage","Fixed")
        Ensure-Field -List "Invoices" -DisplayName "Discount Value"  -InternalName "InvDiscountValue"  -Type Number
        Ensure-Field -List "Invoices" -DisplayName "Subtotal (Net)"  -InternalName "InvSubtotalNet"    -Type Currency
        Ensure-Field -List "Invoices" -DisplayName "Discount Amount" -InternalName "InvDiscountAmount" -Type Currency

        Ensure-View -List "Invoices" -Title "All Invoices" -Fields @(
            "FileLeafRef","InvoiceNumber","InvCustomer","POReference","InvDateIssued","InvStatus",
            "InvSubtotalNet","InvDiscountType","InvDiscountValue","InvDiscountAmount",
            "InvAmountNet","InvVATAmount","InvAmountGross","TaxYear","FinancialYear"
        )
    }

    # =======================
    # Receipts
    # =======================
    Ensure-Field -List "Receipts" -DisplayName "Supplier"       -InternalName "RecSupplier"   -Type Lookup -LookupList "Suppliers" -LookupField "Title"
    Ensure-Field -List "Receipts" -DisplayName "Category"       -InternalName "RecCategory"   -Type Choice -Choices @("Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Receipts" -DisplayName "Date"           -InternalName "RecDate"       -Type DateTime
    Ensure-Field -List "Receipts" -DisplayName "Amount Gross"   -InternalName "RecAmountGross" -Type Currency
    Ensure-Field -List "Receipts" -DisplayName "Ledger Item ID" -InternalName "RecLedgerId"   -Type Number
    Ensure-Field -List "Receipts" -DisplayName "Tax Year"       -InternalName "TaxYear"       -Type Text
    Ensure-Field -List "Receipts" -DisplayName "Financial Year" -InternalName "FinancialYear" -Type Text

    Ensure-View -List "Receipts" -Title "All Receipts" -Fields @("FileLeafRef","RecSupplier","RecDate","RecAmountGross","RecLedgerId","RecCategory","TaxYear","FinancialYear")

    # =======================
    # Company Settings (seeded)
    # =======================
    Ensure-List -Title "Company Settings" -Template "GenericList" -EnableAttachments:$false -Url "CompanySettings" -OnQuickLaunch

    # Identity & contact
    Ensure-Field -List "Company Settings" -DisplayName "Profile Name"       -InternalName "Title"                 -Type Text -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Company Settings" -DisplayName "Business Name"      -InternalName "BusinessName"          -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Trading Name"       -InternalName "TradingName"           -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Contact Name"       -InternalName "ContactName"           -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Invoicing Email"    -InternalName "InvoicingEmail"        -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Invoicing Phone"    -InternalName "InvoicingPhone"        -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Registered Address" -InternalName "RegisteredAddress"     -Type Note

    # Registration & tax
    Ensure-Field -List "Company Settings" -DisplayName "Company Reg No"     -InternalName "CompanyRegNo"          -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "VAT Reg No"         -InternalName "VATRegNo"              -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Default VAT Rate"   -InternalName "DefaultVATRate"        -Type Number
    Ensure-Field -List "Company Settings" -DisplayName "Base Currency"      -InternalName "BaseCurrency"          -Type Text

    # FY boundary
    Ensure-Field -List "Company Settings" -DisplayName "FY Start Month"     -InternalName "FYStartMonth"          -Type Number
    Ensure-Field -List "Company Settings" -DisplayName "FY Start Day"       -InternalName "FYStartDay"            -Type Number

    # Invoice numbering & payment terms
    Ensure-Field -List "Company Settings" -DisplayName "Invoice Prefix"     -InternalName "InvoicePrefix"         -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Next Invoice No"    -InternalName "NextInvoiceNo"         -Type Number
    Ensure-Field -List "Company Settings" -DisplayName "Payment Terms (days)" -InternalName "PaymentTermsDays"    -Type Number

    # Bank details (optional)
    Ensure-Field -List "Company Settings" -DisplayName "Bank Account Name"  -InternalName "BankAccountName"       -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Sort Code"          -InternalName "BankSortCode"          -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "Account Number"     -InternalName "BankAccountNumber"     -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "IBAN"               -InternalName "BankIBAN"              -Type Text
    Ensure-Field -List "Company Settings" -DisplayName "SWIFT/BIC"          -InternalName "BankSWIFT"             -Type Text

    # Seed a single default item if none exists
    try {
        $existingCompanyItem = Get-PnPListItem -List "Company Settings" -PageSize 1 | Select-Object -First 1
        if (-not $existingCompanyItem) {
            Add-PnPListItem -List "Company Settings" -Values @{
                Title            = "Default"
                BusinessName     = "Your Business Ltd"
                BaseCurrency     = $baseCurrencyCode
                DefaultVATRate   = 20
                FYStartMonth     = 1
                FYStartDay       = 1
                PaymentTermsDays = 14
            } | Out-Null
            Write-Host "✓ Seeded Company Settings (Profile: Default)" -ForegroundColor Green
        }
    } catch {
        Write-Warning ("Could not seed Company Settings: {0}" -f $_.Exception.Message)
    }

    # Apply currency LCID to all Currency fields
    $currencyTargets = @(
        @{ List="Customers"; Fields=@("DefaultDayRate") },
        @{ List="Ledger";   Fields=@("AmountNet","VATAmount","AmountGross") },
        @{ List="Invoices"; Fields=@("InvAmountNet","InvVATAmount","InvAmountGross") },
        @{ List="Receipts"; Fields=@("RecAmountGross") }
    )
    if ($enableDiscounts) {
        $currencyTargets += @{ List="Invoices"; Fields=@("InvSubtotalNet","InvDiscountAmount") }
    }
    foreach ($t in $currencyTargets) {
        foreach ($f in $t.Fields) { Set-CurrencyOnField -List $t.List -FieldInternalName $f -CurrencyLcid $currencyLcid }
    }

    Write-Host "`n✓ FinanceHub Provisioning Complete" -ForegroundColor Green
    Write-Host "Site URL: $siteUrl"
    Write-Host "Regional: LocaleId=$localeId, TimeZone='$timeZoneName', FirstDay=$firstDay, 24h=$use24hBool"
    Write-Host "Currency: $baseCurrencyCode (LCID $currencyLcid)"
    Write-Host "Config: $configFile"

    Disconnect-PnPOnline | Out-Null
    exit 0
}
catch {
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    try { Disconnect-PnPOnline | Out-Null } catch {}
    exit 1
}