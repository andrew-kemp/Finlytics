<#
    Provision-AccountingHub.ps1
    Fully interactive, repeatable provisioning of a branded Accounting Hub (LedgerHub/FinanceHub) in SharePoint Online.

    Prompts for:
      - Tenant short name (e.g., kempy)
      - Company display name (e.g., AK)
      - Product suffix (LedgerHub/FinanceHub)
      - Site alias (URL name), default derived (e.g., ak-ledgerhub or aklh)

    Derives:
      - TenantId: <tenantShort>.onmicrosoft.com
      - RootUrl:  https://<tenantShort>.sharepoint.com
      - AdminUrl: https://<tenantShort>-admin.sharepoint.com
      - SiteUrl:  <RootUrl>/sites/<SiteAlias>

    Auth flow:
      - Uses PnP PowerShell to create Entra App + certificate (Register-PnPAzureADApp)
      - Opens admin consent URL for you
      - Connects to SPO Admin and Site using certificate auth

    Notes:
      - Microsoft Graph connection is OPTIONAL (many machines currently have MSAL conflicts).
        This script does not require Graph to succeed.
#>

param(
    [switch]$ForceRecreateApp
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "acct-hub-config.ini"

# ===========================
# INI helpers
# ===========================
function Get-IniContent {
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
            $name = $matches[1].Trim()
            $value = $matches[2]
            $ini[$section][$name] = $value
        }
    }

    return $ini
}

function Set-IniValue {
    param(
        [string]$Path,
        [string]$Section,
        [string]$Key,
        [string]$Value
    )

    if (-not (Test-Path $Path)) {
        "# Accounting Hub Configuration" | Set-Content $Path -Encoding UTF8
    }

    $content = Get-Content $Path -ErrorAction SilentlyContinue
    $inSection = $false
    $found = $false
    $sectionExists = $false
    $newContent = @()

    $keyPattern = "^\s*" + [regex]::Escape($Key) + "\s*="

    foreach ($line in $content) {
        if ($line -match "^\[$Section\]") {
            $inSection = $true
            $sectionExists = $true
            $newContent += $line
        }
        elseif ($line -match "^\[.*\]") {
            if ($inSection -and -not $found) {
                $newContent += "$Key=$Value"
                $found = $true
            }
            $inSection = $false
            $newContent += $line
        }
        elseif ($inSection -and $line -match $keyPattern) {
            $newContent += "$Key=$Value"
            $found = $true
        }
        else {
            $newContent += $line
        }
    }

    if ($inSection -and -not $found) {
        $newContent += "$Key=$Value"
        $found = $true
    }

    if (-not $sectionExists) {
        $newContent += ""
        $newContent += "[$Section]"
        $newContent += "$Key=$Value"
    }

    $newContent | Set-Content -Path $Path -Force -Encoding UTF8
}

function Get-IniValueOrPrompt {
    param(
        [string]$Path,
        [string]$Section,
        [string]$Key,
        [string]$Prompt,
        [string]$Default = ""
    )

    $config = Get-IniContent -Path $Path
    $value = $null
    if ($config.ContainsKey($Section)) { $value = $config[$Section][$Key] }

    if ([string]::IsNullOrWhiteSpace($value)) {
        if ([string]::IsNullOrWhiteSpace($Default)) {
            $value = Read-Host $Prompt
        } else {
            $input = Read-Host "$Prompt [$Default]"
            $value = if ([string]::IsNullOrWhiteSpace($input)) { $Default } else { $input }
        }
        Set-IniValue -Path $Path -Section $Section -Key $Key -Value $value
    }

    return $value
}

# ===========================
# Utility helpers
# ===========================
function New-Slug {
    param([string]$Text)
    $t = $Text.ToLowerInvariant()
    $t = $t -replace "[^a-z0-9]+","-"
    $t.Trim("-")
}

function Pause-ForPropagation {
    param([int]$Seconds = 45,[string]$Message = "Waiting for permissions to propagate")
    Write-Host "$Message..." -ForegroundColor Yellow
    Start-Sleep -Seconds $Seconds
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

function Ensure-Module {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [string]$MinVersion = "0.0.0"
    )

    $installed = Get-Module -ListAvailable -Name $Name |
        Where-Object { $_.Version -ge [version]$MinVersion } |
        Select-Object -First 1

    if (-not $installed) {
        Write-Host "Installing $Name (min $MinVersion)..." -ForegroundColor Yellow
        Install-Module -Name $Name -MinimumVersion $MinVersion -Scope CurrentUser -Force -AllowClobber
    } else {
        Write-Host "✓ $Name v$($installed.Version) already installed" -ForegroundColor Green
    }
}

# ===========================
# Main
# ===========================
try {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Accounting Hub Provisioner" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    if (-not (Test-Path $configFile)) {
        Write-Host "Creating new configuration file: $configFile" -ForegroundColor Yellow
        "# Accounting Hub Configuration" | Set-Content $configFile -Encoding UTF8
    }

    # ---------------------------------------
    # Part 1: Prereqs
    # ---------------------------------------
    Write-Host "`n--- Prerequisites ---`n" -ForegroundColor Cyan
    Write-Host "Checking PowerShell version..." -ForegroundColor Yellow
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        throw "PowerShell 7+ is required. Current: $($PSVersionTable.PSVersion)"
    }
    Write-Host "✓ PowerShell $($PSVersionTable.PSVersion) detected" -ForegroundColor Green

    Write-Host "`nEnsuring required modules..." -ForegroundColor Yellow
    Ensure-Module -Name "PnP.PowerShell" -MinVersion "2.0.0"

    # Graph is optional; install but do not block script if Graph auth is broken on machine.
    Ensure-Module -Name "Microsoft.Graph.Authentication" -MinVersion "2.0.0" | Out-Null

    Import-Module PnP.PowerShell -ErrorAction Stop
    try { Import-Module Microsoft.Graph.Authentication -ErrorAction Stop } catch {}

    # ---------------------------------------
    # Part 2: Fully interactive prompts (tenant + site naming)
    # ---------------------------------------
    Write-Host "`n--- Tenant & Site Configuration ---`n" -ForegroundColor Cyan

    $tenantShort = Get-IniValueOrPrompt -Path $configFile -Section "Tenant" -Key "ShortName" `
        -Prompt "Tenant short name (e.g., kempy)"

    $tenantId = "$tenantShort.onmicrosoft.com"
    $rootUrl  = "https://$tenantShort.sharepoint.com"
    $adminUrl = "https://$tenantShort-admin.sharepoint.com"

    Set-IniValue -Path $configFile -Section "Tenant" -Key "TenantId" -Value $tenantId
    Set-IniValue -Path $configFile -Section "Tenant" -Key "RootUrl"  -Value $rootUrl
    Set-IniValue -Path $configFile -Section "Tenant" -Key "AdminUrl" -Value $adminUrl

    Write-Host "Derived:" -ForegroundColor Gray
    Write-Host "  TenantId: $tenantId" -ForegroundColor Gray
    Write-Host "  RootUrl:  $rootUrl" -ForegroundColor Gray
    Write-Host "  AdminUrl: $adminUrl" -ForegroundColor Gray

    $companyName = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "CompanyName" `
        -Prompt "Company display name (e.g., AK)" -Default "AK"

    $autoInitials = Get-InitialsFromName -Name $companyName -MaxLen 5
    $companyInitials = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "CompanyInitials" `
        -Prompt "Company initials/prefix" -Default $autoInitials

    $productSuffixDefault = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "ProductSuffix" `
        -Prompt "Product (LedgerHub or FinanceHub)" -Default "LedgerHub"
    $productSuffix = Read-Choice -PromptText "Confirm product" -Options @("LedgerHub","FinanceHub") -Default $productSuffixDefault
    Set-IniValue -Path $configFile -Section "Brand" -Key "ProductSuffix" -Value $productSuffix

    $brandShort = "$companyInitials $productSuffix" # Display title default
    $siteTitle = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteTitle" `
        -Prompt "SharePoint Site Title" -Default $brandShort

    # Let user choose a “URL name” explicitly (this is what you asked for)
    # Provide two suggested defaults: AKLedgerHub and ak-ledgerhub
    $suggestNoDash = ($companyInitials + $productSuffix) -replace '\s+',''
    $suggestSlug   = New-Slug $brandShort

    $siteAliasStyle = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "AliasStyle" `
        -Prompt "Site URL name style (NoDashes or Slug)" -Default "NoDashes"

    if ($siteAliasStyle -match "slug") {
        $siteAliasDefault = $suggestSlug
    } else {
        # SPO aliases are case-insensitive; we store lower for consistency
        $siteAliasDefault = $suggestNoDash.ToLowerInvariant()
    }

    $siteAlias = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteAlias" `
        -Prompt "Site URL name (this becomes /sites/<name>)" -Default $siteAliasDefault

    $ownerUpn = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "OwnerUpn" `
        -Prompt "Site owner UPN (e.g., andy@$tenantShort.com)"

    # Always Team site by default
    $siteType = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteType" `
        -Prompt "Site type (Team or Communication)" -Default "Team"
    if ($siteType -notmatch '^(Team|Communication)$') { $siteType = "Team" }
    Set-IniValue -Path $configFile -Section "SharePoint" -Key "SiteType" -Value $siteType

    $siteUrl = "$rootUrl/sites/$siteAlias"
    Set-IniValue -Path $configFile -Section "SharePoint" -Key "SiteUrl" -Value $siteUrl

    Write-Host "`nConfiguration Summary:" -ForegroundColor Cyan
    Write-Host "  Tenant:     $tenantShort" -ForegroundColor Gray
    Write-Host "  Site Title: $siteTitle" -ForegroundColor Gray
    Write-Host "  Site URL:   $siteUrl" -ForegroundColor Gray
    Write-Host "  Owner UPN:  $ownerUpn" -ForegroundColor Gray
    Write-Host "  Product:    $productSuffix" -ForegroundColor Gray

    # ---------------------------------------
    # Part 3: OPTIONAL Graph connectivity (non-blocking)
    # ---------------------------------------
    Write-Host "`n--- Microsoft Graph (Optional) ---`n" -ForegroundColor Cyan
    $tryGraph = Read-Host "Attempt Graph login now? (Y/N) [N]"
    if ($tryGraph -match '^[Yy]') {
        try {
            Write-Host "Disconnecting any existing Graph session..." -ForegroundColor Gray
            try { Disconnect-MgGraph -ErrorAction SilentlyContinue | Out-Null } catch {}
            Write-Host "Connecting to Microsoft Graph (Tenant: $tenantId)..." -ForegroundColor Yellow
            Connect-MgGraph -TenantId $tenantId -Scopes "Group.ReadWrite.All" -ErrorAction Stop
            $mgContext = Get-MgContext
            Write-Host "✓ Connected to Microsoft Graph" -ForegroundColor Green
            Write-Host "  Account: $($mgContext.Account)" -ForegroundColor Gray
            Write-Host "  Tenant:  $($mgContext.TenantId)" -ForegroundColor Gray
        } catch {
            Write-Host "⚠ Graph login failed on this machine. Continuing without Graph." -ForegroundColor Yellow
            Write-Host "  $($_.Exception.Message)" -ForegroundColor DarkGray
        }
    } else {
        Write-Host "Skipping Graph login." -ForegroundColor Gray
    }

    # ---------------------------------------
    # Part 4: App registration + certificate (PnP)
    # ---------------------------------------
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "App Registration & Certificate (PnP)" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    $appName = Get-IniValueOrPrompt -Path $configFile -Section "App" -Key "AppName" `
        -Prompt "App registration display name" -Default "AccountingHub-PnP"

    $certFolder = Join-Path $scriptRoot "acct-hub-cert"
    if (-not (Test-Path $certFolder)) { New-Item -ItemType Directory -Path $certFolder -Force | Out-Null }

    $cfg = Get-IniContent -Path $configFile
    $clientId = if ($cfg.ContainsKey("App")) { $cfg["App"]["ClientId"] } else { $null }
    $pfxPath  = if ($cfg.ContainsKey("App")) { $cfg["App"]["PfxPath"] } else { $null }
    $thumb    = if ($cfg.ContainsKey("App")) { $cfg["App"]["Thumbprint"] } else { $null }

    if ($ForceRecreateApp -or [string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($pfxPath) -or -not (Test-Path $pfxPath)) {
        Write-Host "Creating Entra App Registration (browser login will open)..." -ForegroundColor Cyan
        Write-Host "Certificate password will be used to secure the PFX file." -ForegroundColor Yellow

        $match = $false
        do {
            $certPassword = Read-Host -AsSecureString "Enter a password for the certificate (keep it safe)"
            $certPasswordConfirm = Read-Host -AsSecureString "Confirm certificate password"

            $bstr1 = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($certPassword)
            $bstr2 = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($certPasswordConfirm)
            try {
                $pwd1 = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr1)
                $pwd2 = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr2)

                if ([string]::IsNullOrWhiteSpace($pwd1) -or [string]::IsNullOrWhiteSpace($pwd2)) {
                    Write-Warning "Password cannot be empty. Please try again."
                    $match = $false
                    continue
                }

                if ($pwd1 -cne $pwd2) {
                    Write-Warning "Passwords do not match (case-sensitive). Please try again."
                    $match = $false
                } else {
                    Write-Host "Password confirmed." -ForegroundColor Green
                    $match = $true
                }
            } finally {
                [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr1)
                [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr2)
            }
        } while (-not $match)

        $regOutput = Register-PnPAzureADApp `
            -ApplicationName $appName `
            -Tenant $tenantId `
            -Store CurrentUser `
            -OutPath $certFolder `
            -GraphApplicationPermissions "Group.ReadWrite.All","User.Read.All","Sites.ReadWrite.All" `
            -SharePointApplicationPermissions "Sites.FullControl.All" `
            -ErrorAction Stop

        $clientId = $regOutput.'AzureAppId/ClientId'
        if (-not $clientId) { $clientId = $regOutput.AzureAppId }
        if (-not $clientId) { $clientId = $regOutput.ClientId }

        Write-Host "✓ App Registration created!" -ForegroundColor Green
        Write-Host "  Client ID: $clientId" -ForegroundColor Gray

        Write-Host "`nWaiting 60 seconds for permissions to propagate..." -ForegroundColor Yellow
        Start-Sleep -Seconds 60

        $storeCert = Get-ChildItem Cert:\CurrentUser\My |
            Where-Object { $_.Subject -like "*CN=$appName*" -and $_.HasPrivateKey } |
            Sort-Object NotAfter -Descending |
            Select-Object -First 1

        if (-not $storeCert) {
            $storeCert = Get-ChildItem Cert:\CurrentUser\My |
                Where-Object { $_.Subject -like "*$appName*" -and $_.HasPrivateKey } |
                Sort-Object NotAfter -Descending |
                Select-Object -First 1
        }

        if (-not $storeCert) { throw "Could not find certificate for '$appName' in CurrentUser store." }

        $pfxPath = Join-Path $certFolder "$appName.pfx"
        Export-PfxCertificate -Cert $storeCert -FilePath $pfxPath -Password $certPassword -Force | Out-Null
        $thumb = $storeCert.Thumbprint

        Set-IniValue -Path $configFile -Section "App" -Key "ClientId"   -Value $clientId
        Set-IniValue -Path $configFile -Section "App" -Key "PfxPath"    -Value $pfxPath
        Set-IniValue -Path $configFile -Section "App" -Key "Thumbprint" -Value $thumb

        Write-Host "✓ Certificate exported: $pfxPath" -ForegroundColor Green
        Write-Host "  Thumbprint: $thumb" -ForegroundColor Gray

        $adminConsentUrl = "https://login.microsoftonline.com/$tenantId/adminconsent?client_id=$clientId"
        Write-Host "`nAdmin consent is required. Opening URL..." -ForegroundColor Yellow
        Write-Host "  $adminConsentUrl" -ForegroundColor Cyan
        try { Start-Process $adminConsentUrl | Out-Null } catch {}

        Read-Host "After granting consent in browser, press Enter to continue"
        Pause-ForPropagation -Seconds 45 -Message "Waiting for app permissions"
    } else {
        Write-Host "✓ Using existing app registration and certificate from config." -ForegroundColor Green
        Write-Host "  ClientId: $clientId" -ForegroundColor Gray
        Write-Host "  PfxPath:  $pfxPath" -ForegroundColor Gray
        Write-Host "  Thumb:    $thumb" -ForegroundColor Gray
    }

    # ---------------------------------------
    # Part 5: Connect to SharePoint Admin
    # ---------------------------------------
    Write-Host "`n--- Connecting to SharePoint Admin ---`n" -ForegroundColor Cyan
    try {
        Connect-PnPOnline -Url $adminUrl -ClientId $clientId -Tenant $tenantId -Thumbprint $thumb -ErrorAction Stop
    } catch {
        Write-Host "Thumbprint auth failed. Falling back to PFX password..." -ForegroundColor Yellow
        $securePwd = Read-Host -AsSecureString "Enter the PFX password to authenticate to SharePoint Admin"
        Connect-PnPOnline -Url $adminUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $securePwd -ErrorAction Stop
    }
    Write-Host "✓ Connected to SharePoint Admin" -ForegroundColor Green

    # ---------------------------------------
    # Part 6: Ensure site exists (Team by default)
    # ---------------------------------------
    Write-Host "`n--- Ensuring Site Exists ---`n" -ForegroundColor Cyan
    $tenantSite = Get-PnPTenantSite -Url $siteUrl -ErrorAction SilentlyContinue

    if (-not $tenantSite) {
        if ($siteType -match "comm") {
            Write-Host "Creating Communication site..." -ForegroundColor Yellow
            New-PnPSite -Type CommunicationSite -Title $siteTitle -Url $siteUrl -Owner $ownerUpn -Wait
        } else {
            Write-Host "Creating Group-connected Team site..." -ForegroundColor Yellow
            New-PnPSite -Type TeamSite -Alias $siteAlias -Title $siteTitle -Owners $ownerUpn -IsPublic:$false | Out-Null
        }

        Write-Host "Waiting for site to be ready..." -ForegroundColor Yellow
        for ($i = 0; $i -lt 12; $i++) {
            Start-Sleep -Seconds 10
            $tenantSite = Get-PnPTenantSite -Url $siteUrl -ErrorAction SilentlyContinue
            if ($tenantSite) { break }
        }
        if (-not $tenantSite) { throw "Site did not appear ready in time: $siteUrl" }
        Write-Host "✓ Site created" -ForegroundColor Green
    } else {
        Write-Host "✓ Site already exists" -ForegroundColor Green
    }

    Disconnect-PnPOnline

    # ---------------------------------------
    # Part 7: Connect to site + provision artefacts
    # ---------------------------------------
    Write-Host "`n--- Connecting to Site ---`n" -ForegroundColor Cyan
    try {
        Connect-PnPOnline -Url $siteUrl -ClientId $clientId -Tenant $tenantId -Thumbprint $thumb -ErrorAction Stop
    } catch {
        Write-Host "Thumbprint auth failed. Falling back to PFX password..." -ForegroundColor Yellow
        $securePwd2 = Read-Host -AsSecureString "Enter the PFX password to authenticate to the site"
        Connect-PnPOnline -Url $siteUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $securePwd2 -ErrorAction Stop
    }
    Write-Host "✓ Connected to site" -ForegroundColor Green

    function Ensure-List {
        param([string]$Title,[string]$Template="GenericList",[bool]$EnableAttachments=$true)
        $l = Get-PnPList -Identity $Title -ErrorAction SilentlyContinue
        if (-not $l) {
            Write-Host "Creating list/library: $Title" -ForegroundColor Yellow
            Add-PnPList -Title $Title -Template $Template -EnableAttachments:$EnableAttachments | Out-Null
        } else {
            Write-Host "✓ Exists: $Title" -ForegroundColor Green
        }
    }

    function Ensure-Field {
        param(
            [string]$List,[string]$DisplayName,[string]$InternalName,
            [ValidateSet("Text","Note","Number","Currency","DateTime","Choice","Boolean","URL","Lookup")]$Type,
            [string[]]$Choices = $null,[string]$LookupList = $null,[string]$LookupField = "Title",
            [bool]$Required=$false,[bool]$AddToDefaultView=$true,[bool]$EnforceUnique=$false,[string]$AdditionalXml=$null
        )
        $exists = Get-PnPField -List $List -Identity $InternalName -ErrorAction SilentlyContinue
        if ($exists) { return }

        if ($AdditionalXml) {
            Add-PnPField -List $List -FieldXml $AdditionalXml | Out-Null
        } elseif ($Type -eq "Choice" -and $Choices) {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type Choice -Choices $Choices -AddToDefaultView:$AddToDefaultView | Out-Null
        } elseif ($Type -eq "Lookup" -and $LookupList) {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type Lookup -LookupList $LookupList -LookupField $LookupField -AddToDefaultView:$AddToDefaultView | Out-Null
        } else {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type $Type -AddToDefaultView:$AddToDefaultView | Out-Null
        }

        if ($Required)      { Set-PnPField -List $List -Identity $InternalName -Values @{Required=$true} | Out-Null }
        if ($EnforceUnique) { Set-PnPField -List $List -Identity $InternalName -Values @{EnforceUniqueValues=$true} | Out-Null }
    }

    function Ensure-View {
        param([string]$List,[string]$Title,[string[]]$Fields,[string]$Query=$null,[int]$RowLimit=50)
        $v = Get-PnPView -List $List -Identity $Title -ErrorAction SilentlyContinue
        if (-not $v) {
            Add-PnPView -List $List -Title $Title -Fields $Fields -RowLimit $RowLimit -Query $Query | Out-Null
            Write-Host "Created view: $Title (List: $List)" -ForegroundColor Yellow
        }
    }

    Write-Host "`n--- Provisioning Lists and Libraries ---`n" -ForegroundColor Cyan

    # Customers
    Ensure-List -Title "Customers" -Template "GenericList" -EnableAttachments:$false
    Set-PnPField -List "Customers" -Identity "Title" -Values @{Title="Customer Name"; Required=$true} | Out-Null
    Ensure-Field -List "Customers" -DisplayName "Customer Code" -InternalName "CustomerCode" -Type Text -Required:$true -EnforceUnique:$true
    Ensure-Field -List "Customers" -DisplayName "Address" -InternalName "Address" -Type Note
    Ensure-Field -List "Customers" -DisplayName "Contact Name" -InternalName "ContactName" -Type Text
    Ensure-Field -List "Customers" -DisplayName "Email" -InternalName "Email" -Type Text
    Ensure-View  -List "Customers" -Title "All Customers" -Fields @("Title","CustomerCode","ContactName","Email")

    # Suppliers
    Ensure-List -Title "Suppliers" -Template "GenericList" -EnableAttachments:$false
    Set-PnPField -List "Suppliers" -Identity "Title" -Values @{Title="Supplier Name"; Required=$true} | Out-Null
    Ensure-Field -List "Suppliers" -DisplayName "Category" -InternalName "SupplierCategory" -Type Choice -Choices @("Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Suppliers" -DisplayName "Default VAT Rate" -InternalName "DefaultVATRate" -Type Number
    Ensure-Field -List "Suppliers" -DisplayName "Email" -InternalName "SupplierEmail" -Type Text
    Ensure-View  -List "Suppliers" -Title "All Suppliers" -Fields @("Title","SupplierCategory","DefaultVATRate","SupplierEmail")

    # Ledger
    Ensure-List -Title "Ledger" -Template "GenericList" -EnableAttachments:$false
    Set-PnPField -List "Ledger" -Identity "Title" -Values @{Title="Entry Name"; Required=$false} | Out-Null
    Ensure-Field -List "Ledger" -DisplayName "Type" -InternalName "EntryType" -Type Choice -Choices @("Income","Expense") -Required:$true
    Ensure-Field -List "Ledger" -DisplayName "Date" -InternalName "EntryDate" -Type DateTime -Required:$true
    Ensure-Field -List "Ledger" -DisplayName "Customer" -InternalName "Customer" -Type Lookup -LookupList "Customers" -LookupField "Title"
    Ensure-Field -List "Ledger" -DisplayName "Supplier" -InternalName "Supplier" -Type Lookup -LookupList "Suppliers" -LookupField "Title"
    Ensure-Field -List "Ledger" -DisplayName "Category" -InternalName "Category" -Type Choice -Choices @("Sales","Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Ledger" -DisplayName "Amount (net)" -InternalName "AmountNet" -Type Currency -Required:$true
    Ensure-Field -List "Ledger" -DisplayName "VAT Amount" -InternalName "VATAmount" -Type Currency
    $calcGrossXml = '<Field Type="Calculated" DisplayName="Amount (gross)" Name="AmountGross" ResultType="Currency"><Formula>=[AmountNet]+[VATAmount]</Formula></Field>'
    Ensure-Field -List "Ledger" -DisplayName "Amount (gross)" -InternalName "AmountGross" -Type Number -AdditionalXml $calcGrossXml
    Ensure-Field -List "Ledger" -DisplayName "Payment Method" -InternalName "PaymentMethod" -Type Choice -Choices @("Bank","Card","Cash","DLA","Other")
    Ensure-Field -List "Ledger" -DisplayName "DLA" -InternalName "IsDLA" -Type Boolean
    Ensure-Field -List "Ledger" -DisplayName "Related Document" -InternalName "RelatedDocument" -Type URL
    Ensure-Field -List "Ledger" -DisplayName "Notes" -InternalName "Notes" -Type Note

    Ensure-View -List "Ledger" -Title "Income" -Fields @("EntryDate","Customer","Category","AmountNet","VATAmount","AmountGross","RelatedDocument","Notes") `
        -Query '<Where><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Income</Value></Eq></Where>'

    Ensure-View -List "Ledger" -Title "Expenses" -Fields @("EntryDate","Supplier","Category","AmountNet","VATAmount","AmountGross","RelatedDocument","Notes") `
        -Query '<Where><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Expense</Value></Eq></Where>'

    # Invoices library
    Ensure-List -Title "Invoices" -Template "DocumentLibrary" -EnableAttachments:$false
    Ensure-Field -List "Invoices" -DisplayName "Invoice Number" -InternalName "InvoiceNumber" -Type Text -Required:$true -EnforceUnique:$true
    Ensure-Field -List "Invoices" -DisplayName "Customer" -InternalName "InvCustomer" -Type Lookup -LookupList "Customers" -LookupField "Title"
    Ensure-Field -List "Invoices" -DisplayName "Amount (net)" -InternalName "InvAmountNet" -Type Currency
    Ensure-Field -List "Invoices" -DisplayName "VAT Amount" -InternalName "InvVATAmount" -Type Currency
    $invGrossXml = '<Field Type="Calculated" DisplayName="Amount (gross)" Name="InvAmountGross" ResultType="Currency"><Formula>=[InvAmountNet]+[InvVATAmount]</Formula></Field>'
    Ensure-Field -List "Invoices" -DisplayName "Amount (gross)" -InternalName "InvAmountGross" -Type Number -AdditionalXml $invGrossXml
    Ensure-Field -List "Invoices" -DisplayName "Date Issued" -InternalName "InvDateIssued" -Type DateTime
    Ensure-Field -List "Invoices" -DisplayName "Date Paid" -InternalName "InvDatePaid" -Type DateTime
    Ensure-Field -List "Invoices" -DisplayName "Status" -InternalName "InvStatus" -Type Choice -Choices @("Draft","Issued","Paid","Overdue")
    Ensure-View  -List "Invoices" -Title "All Invoices" -Fields @("InvoiceNumber","InvCustomer","InvDateIssued","InvStatus","InvAmountNet","InvVATAmount","InvAmountGross")

    # Receipts library
    Ensure-List -Title "Receipts" -Template "DocumentLibrary" -EnableAttachments:$false
    Ensure-Field -List "Receipts" -DisplayName "Supplier" -InternalName "RecSupplier" -Type Lookup -LookupList "Suppliers" -LookupField "Title"
    Ensure-Field -List "Receipts" -DisplayName "Category" -InternalName "RecCategory" -Type Choice -Choices @("Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Receipts" -DisplayName "Date" -InternalName "RecDate" -Type DateTime
    Ensure-Field -List "Receipts" -DisplayName "Amount (gross)" -InternalName "RecAmountGross" -Type Currency
    Ensure-Field -List "Receipts" -DisplayName "Ledger Item ID" -InternalName "RecLedgerId" -Type Number
    Ensure-View  -List "Receipts" -Title "All Receipts" -Fields @("FileLeafRef","RecSupplier","RecDate","RecAmountGross","RecLedgerId","RecCategory")

    Write-Host "`n✓ Provisioning complete." -ForegroundColor Green
    Write-Host "Site URL: $siteUrl" -ForegroundColor Gray

    # Optional: Team creation
    Write-Host "`n--- Optional: Microsoft Teams ---`n" -ForegroundColor Cyan
    try {
        $group = Get-PnPMicrosoft365Group -Site $siteUrl -ErrorAction SilentlyContinue
        if ($group) {
            $ans = Read-Host "Create a Microsoft Team now for this site? (Y/N) [N]"
            if ($ans -match "^[Yy]") {
                New-PnPTeamsTeam -Group $group.Id -Visibility Private | Out-Null
                Write-Host "✓ Microsoft Team created." -ForegroundColor Green
            } else {
                Write-Host "Skipping Teams creation." -ForegroundColor Gray
            }
        } else {
            Write-Host "No M365 Group found for site. Teams step skipped." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Teams creation skipped (can run later with New-PnPTeamsTeam)." -ForegroundColor Yellow
    }

    # Disconnect
    try { Disconnect-PnPOnline -ErrorAction SilentlyContinue | Out-Null } catch {}
    try { Disconnect-MgGraph -ErrorAction SilentlyContinue | Out-Null } catch {}

    exit 0
}
catch {
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    try { Disconnect-PnPOnline -ErrorAction SilentlyContinue | Out-Null } catch {}
    try { Disconnect-MgGraph -ErrorAction SilentlyContinue | Out-Null } catch {}
    exit 1
}