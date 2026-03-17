<#
    Provision-FinanceHub.ps1
    Resume-safe, interactive provisioning of a Finance/Ledger Hub in SharePoint Online.

    Persisted config: finance-hub-config.ini
      - Reuses existing app registration + cert (ClientId, Thumbprint, PfxPath)
      - Skips site creation if it already exists
      - Skips list/library creation if already present

    Notes:
      - Uses PnP.PowerShell only (no Connect-MgGraph).
      - New-PnPList in PnP.PowerShell v3.x does NOT accept -EnableAttachments; we set attachments via Set-PnPList.
      - Unique columns require indexing; Ensure-Field enforces Indexed first when EnforceUnique is requested.
      - Does NOT store the PFX password in INI (will prompt on reruns).
#>

param(
    [switch]$ForceRecreateApp
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

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
        "# Finance Hub Configuration" | Set-Content $Path -Encoding UTF8
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
        [string]$Default = "",
        [switch]$AllowBlank
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

        if (-not $AllowBlank) {
            while ([string]::IsNullOrWhiteSpace($value)) {
                $value = Read-Host $Prompt
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($value)) {
            Set-IniValue -Path $Path -Section $Section -Key $Key -Value $value
        }
    }

    return $value
}

# ===========================
# Utility helpers
# ===========================
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
    param([string]$Name,[string]$MinVersion="0.0.0")
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

function Pause-ForPropagation {
    param([int]$Seconds = 45,[string]$Message = "Waiting for permissions to propagate")
    Write-Host "$Message..." -ForegroundColor Yellow
    Start-Sleep -Seconds $Seconds
}

function Ensure-List {
    param(
        [string]$Title,
        [string]$Template = "GenericList",
        [bool]$EnableAttachments = $true,
        [string]$Url = $null,
        [switch]$OnQuickLaunch
    )

    $l = Get-PnPList -Identity $Title -ErrorAction SilentlyContinue
    if (-not $l) {
        Write-Host "Creating list/library: $Title" -ForegroundColor Yellow

        if ([string]::IsNullOrWhiteSpace($Url)) {
            $Url = ($Title -replace '\s+', '-')
        }

        $params = @{
            Title    = $Title
            Template = $Template
            Url      = $Url
        }

        if ($OnQuickLaunch) {
            $params["OnQuickLaunch"] = $true
        }

        New-PnPList @params | Out-Null

        if ($Template -eq "GenericList") {
            Set-PnPList -Identity $Title -EnableAttachments:$EnableAttachments | Out-Null
        }
    }
    else {
        Write-Host "✓ Exists: $Title" -ForegroundColor Green
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
        [bool]$Indexed=$false,
        [string]$AdditionalXml=$null
    )

    $exists = Get-PnPField -List $List -Identity $InternalName -ErrorAction SilentlyContinue
    if (-not $exists) {
        if ($AdditionalXml) {
            Add-PnPField -List $List -FieldXml $AdditionalXml | Out-Null
        } elseif ($Type -eq "Choice" -and $Choices) {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type Choice -Choices $Choices -AddToDefaultView:$AddToDefaultView | Out-Null
        } elseif ($Type -eq "Lookup" -and $LookupList) {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type Lookup -LookupList $LookupList -LookupField $LookupField -AddToDefaultView:$AddToDefaultView | Out-Null
        } else {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type $Type -AddToDefaultView:$AddToDefaultView | Out-Null
        }
    }

    # Apply post-settings in correct order
    # Unique values require Indexed = true first
    $needsIndex = $Indexed -or $EnforceUnique
    if ($needsIndex) {
        try {
            Set-PnPField -List $List -Identity $InternalName -Values @{ Indexed = $true } | Out-Null
        } catch {
            throw "Failed to index field '$InternalName' on list '$List'. Unique values require indexing. Underlying error: $($_.Exception.Message)"
        }
    }

    if ($EnforceUnique) {
        Set-PnPField -List $List -Identity $InternalName -Values @{ EnforceUniqueValues = $true } | Out-Null
    }

    if ($Required) {
        Set-PnPField -List $List -Identity $InternalName -Values @{ Required = $true } | Out-Null
    }
}

function Ensure-View {
    param([string]$List,[string]$Title,[string[]]$Fields,[string]$Query=$null,[int]$RowLimit=50)
    $v = Get-PnPView -List $List -Identity $Title -ErrorAction SilentlyContinue
    if (-not $v) {
        Add-PnPView -List $List -Title $Title -Fields $Fields -RowLimit $RowLimit -Query $Query | Out-Null
        Write-Host "Created view: $Title (List: $List)" -ForegroundColor Yellow
    }
}

function Get-YearLabel {
    param([datetime]$StartDate)
    "$($StartDate.Year)-$($StartDate.AddYears(1).Year)"
}

function Get-FinancialYearStart {
    param([datetime]$Date,[int]$StartMonth)
    $candidate = Get-Date -Year $Date.Year -Month $StartMonth -Day 1
    if ($Date -lt $candidate) { $candidate = $candidate.AddYears(-1) }
    $candidate
}

function Get-TaxYearStart_Apr1 {
    param([datetime]$Date)
    $candidate = Get-Date -Year $Date.Year -Month 4 -Day 1
    if ($Date -lt $candidate) { $candidate = $candidate.AddYears(-1) }
    $candidate
}

# ===========================
# Main
# ===========================
try {
    if (-not (Test-Path $configFile)) {
        "# Finance Hub Configuration" | Set-Content $configFile -Encoding UTF8
    }

    Ensure-Module -Name "PnP.PowerShell" -MinVersion "2.0.0"
    Import-Module PnP.PowerShell -ErrorAction Stop

    $tenantShort = Get-IniValueOrPrompt -Path $configFile -Section "Tenant" -Key "ShortName" -Prompt "Tenant short name (e.g., kempy)"
    $tenantId = "$tenantShort.onmicrosoft.com"
    $rootUrl  = "https://$tenantShort.sharepoint.com"
    $adminUrl = "https://$tenantShort-admin.sharepoint.com"
    Set-IniValue -Path $configFile -Section "Tenant" -Key "TenantId" -Value $tenantId
    Set-IniValue -Path $configFile -Section "Tenant" -Key "RootUrl" -Value $rootUrl
    Set-IniValue -Path $configFile -Section "Tenant" -Key "AdminUrl" -Value $adminUrl

    $companyDisplay = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "CompanyDisplay" -Prompt "Company display (e.g., AK)" -Default "AK"
    $product = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "Product" -Prompt "Hub type (LedgerHub or FinanceHub)" -Default "LedgerHub"
    if ($product -notin @("LedgerHub","FinanceHub")) { $product = "LedgerHub"; Set-IniValue -Path $configFile -Section "Brand" -Key "Product" -Value $product }

    $defaultSiteTitle = "$companyDisplay $product"
    $siteTitle = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteTitle" -Prompt "SharePoint Site Title" -Default $defaultSiteTitle

    $defaultAliasNoDashes = (($companyDisplay + $product) -replace '\s+','').ToLowerInvariant()
    $siteAlias = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteAlias" -Prompt "Site URL name (/sites/<name>)" -Default $defaultAliasNoDashes
    $siteUrl = "$rootUrl/sites/$siteAlias"
    Set-IniValue -Path $configFile -Section "SharePoint" -Key "SiteUrl" -Value $siteUrl

    $ownerUpn = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "OwnerUpn" -Prompt "Site Owner UPN (only needed if site must be created)" -AllowBlank

    $fyStartMonthStr = Get-IniValueOrPrompt -Path $configFile -Section "Year" -Key "BusinessFYStartMonth" -Prompt "Business financial year start month (1-12)" -Default "1"
    if ($fyStartMonthStr -notmatch '^(1[0-2]|[1-9])$') { throw "Invalid BusinessFYStartMonth: $fyStartMonthStr" }
    $fyStartMonth = [int]$fyStartMonthStr

    $today = Get-Date
    $taxStart = Get-TaxYearStart_Apr1 -Date $today
    $fyStart  = Get-FinancialYearStart -Date $today -StartMonth $fyStartMonth
    $taxChoices = @((Get-YearLabel $taxStart.AddYears(-1)), (Get-YearLabel $taxStart), (Get-YearLabel $taxStart.AddYears(1))) | Select-Object -Unique
    $fyChoices  = @((Get-YearLabel $fyStart.AddYears(-1)),  (Get-YearLabel $fyStart),  (Get-YearLabel $fyStart.AddYears(1)))  | Select-Object -Unique

    $defaultAppName = "$companyDisplay-$product-PnP"
    $appName = Get-IniValueOrPrompt -Path $configFile -Section "App" -Key "AppName" -Prompt "App registration display name" -Default $defaultAppName
    $appName = $appName -replace '[–—−]', '-'

    $certFolder = Join-Path $scriptRoot "cert-output"
    if (-not (Test-Path $certFolder)) { New-Item -ItemType Directory -Path $certFolder -Force | Out-Null }

    $cfg = Get-IniContent -Path $configFile
    $clientId = if ($cfg.ContainsKey("App")) { $cfg["App"]["ClientId"] } else { $null }
    $pfxPath  = if ($cfg.ContainsKey("App")) { $cfg["App"]["PfxPath"] } else { $null }
    $thumb    = if ($cfg.ContainsKey("App")) { $cfg["App"]["Thumbprint"] } else { $null }
    $certPwdToUse = $null

    if ($ForceRecreateApp -or [string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($pfxPath) -or -not (Test-Path $pfxPath)) {
        Write-Host "`n--- App Registration & Certificate ---`n" -ForegroundColor Cyan

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

        $graphPerms = @("Group.ReadWrite.All","User.Read.All","Sites.ReadWrite.All")
        $spoPerms = @("Sites.FullControl.All")

        $regOutput = Register-PnPAzureADApp `
            -ApplicationName $appName `
            -Tenant $tenantId `
            -Store CurrentUser `
            -OutPath $certFolder `
            -GraphApplicationPermissions $graphPerms `
            -SharePointApplicationPermissions $spoPerms `
            -ErrorAction Stop

        $clientId = $regOutput.'AzureAppId/ClientId'
        if (-not $clientId) { $clientId = $regOutput.AzureAppId }
        if (-not $clientId) { $clientId = $regOutput.ClientId }

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

        Set-IniValue -Path $configFile -Section "App" -Key "ClientId" -Value $clientId
        Set-IniValue -Path $configFile -Section "App" -Key "PfxPath" -Value $pfxPath
        Set-IniValue -Path $configFile -Section "App" -Key "Thumbprint" -Value $thumb

        $adminConsentUrl = "https://login.microsoftonline.com/$tenantId/adminconsent?client_id=$clientId"
        try { Start-Process $adminConsentUrl | Out-Null } catch {}
        Read-Host "After granting consent in browser, press Enter to continue"
        Pause-ForPropagation -Seconds 45 -Message "Waiting for app permissions"

        $certPwdToUse = $certPassword
    }

    if (-not $certPwdToUse) {
        $certPwdToUse = Read-Host -AsSecureString "Enter the PFX password (needed to connect to SharePoint)"
    }

    Write-Host "`nConnecting to SharePoint Admin: $adminUrl" -ForegroundColor Yellow
    Connect-PnPOnline -Url $adminUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $certPwdToUse -ErrorAction Stop
    Write-Host "✓ Connected to SharePoint Admin" -ForegroundColor Green

    $existingSite = Get-PnPTenantSite -Url $siteUrl -ErrorAction SilentlyContinue
    if (-not $existingSite) {
        if ([string]::IsNullOrWhiteSpace($ownerUpn)) {
            $ownerUpn = Read-Host "Site Owner UPN is required to create the site. Enter Site Owner UPN"
            while ([string]::IsNullOrWhiteSpace($ownerUpn)) {
                $ownerUpn = Read-Host "Site Owner UPN cannot be blank. Enter Site Owner UPN"
            }
            Set-IniValue -Path $configFile -Section "SharePoint" -Key "OwnerUpn" -Value $ownerUpn
        }

        New-PnPSite -Type TeamSite -Alias $siteAlias -Title $siteTitle -Owners $ownerUpn -IsPublic:$false | Out-Null

        for ($i = 0; $i -lt 12; $i++) {
            Start-Sleep -Seconds 10
            $existingSite = Get-PnPTenantSite -Url $siteUrl -ErrorAction SilentlyContinue
            if ($existingSite) { break }
        }
        if (-not $existingSite) { throw "Site did not appear ready in time: $siteUrl" }
    } else {
        Write-Host "✓ Site already exists" -ForegroundColor Green
    }

    Disconnect-PnPOnline

    Write-Host "`nConnecting to site: $siteUrl" -ForegroundColor Yellow
    Connect-PnPOnline -Url $siteUrl -ClientId $clientId -Tenant $tenantId -CertificatePath $pfxPath -CertificatePassword $certPwdToUse -ErrorAction Stop
    Write-Host "✓ Connected to site" -ForegroundColor Green

    Ensure-List -Title "Customers" -Template "GenericList" -EnableAttachments:$false -Url "Customers" -OnQuickLaunch
    Ensure-List -Title "Suppliers" -Template "GenericList" -EnableAttachments:$false -Url "Suppliers" -OnQuickLaunch
    Ensure-List -Title "Ledger"    -Template "GenericList" -EnableAttachments:$false -Url "Ledger" -OnQuickLaunch
    Ensure-List -Title "Invoices"  -Template "DocumentLibrary" -Url "Invoices" -OnQuickLaunch
    Ensure-List -Title "Receipts"  -Template "DocumentLibrary" -Url "Receipts" -OnQuickLaunch

    # Customers fields
    Set-PnPField -List "Customers" -Identity "Title" -Values @{Title="Customer Name"; Required=$true} | Out-Null
    Ensure-Field -List "Customers" -DisplayName "Customer Code" -InternalName "CustomerCode" -Type Text -Required:$true -EnforceUnique:$true
    Ensure-Field -List "Customers" -DisplayName "Address" -InternalName "Address" -Type Note
    Ensure-Field -List "Customers" -DisplayName "Contact Name" -InternalName "ContactName" -Type Text
    Ensure-Field -List "Customers" -DisplayName "Email" -InternalName "Email" -Type Text

    # Suppliers fields
    Set-PnPField -List "Suppliers" -Identity "Title" -Values @{Title="Supplier Name"; Required=$true} | Out-Null
    Ensure-Field -List "Suppliers" -DisplayName "Category" -InternalName "SupplierCategory" -Type Choice -Choices @("Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Suppliers" -DisplayName "Default VAT Rate" -InternalName "DefaultVATRate" -Type Number
    Ensure-Field -List "Suppliers" -DisplayName "Email" -InternalName "SupplierEmail" -Type Text

    # Year fields
    foreach ($target in @("Ledger","Invoices","Receipts")) {
        Ensure-Field -List $target -DisplayName "Tax Year" -InternalName "TaxYear" -Type Choice -Choices $taxChoices
        Ensure-Field -List $target -DisplayName "Financial Year" -InternalName "FinancialYear" -Type Choice -Choices $fyChoices
    }

    Write-Host "`n✓ Provisioning complete." -ForegroundColor Green
    Write-Host "Site: $siteUrl" -ForegroundColor Gray
    Write-Host "Config: $configFile" -ForegroundColor Gray

    Disconnect-PnPOnline -ErrorAction SilentlyContinue | Out-Null
    exit 0
}
catch {
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    try { Disconnect-PnPOnline -ErrorAction SilentlyContinue | Out-Null } catch {}
    exit 1
}