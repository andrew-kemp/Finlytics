<#
    Provision-FinanceHub.ps1
    Resume-safe, interactive provisioning of a Finance or Ledger Hub in SharePoint Online.

    Persisted config: finance-hub-config.ini
      - Reuses existing app registration + cert (ClientId, Thumbprint, PfxPath)
      - Skips site creation if it already exists
      - Skips list or library creation if already present

    Notes:
      - Uses PnP.PowerShell only.
      - New-PnPList in recent PnP releases does not accept -EnableAttachments; we set via Set-PnPList post-creation.
      - Unique columns require indexing first; Ensure-Field enforces index before unique.
      - PFX password is not stored in the INI.
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
        Write-Host "Creating list or library: $Title" -ForegroundColor Yellow

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

# Version-safe lookup creation using FieldXml
function Ensure-LookupField {
    param(
        [string]$ListTitle,
        [string]$DisplayName,
        [string]$InternalName,
        [string]$LookupListTitle,
        [string]$LookupField = "Title",
        [bool]$Required = $false,
        [bool]$AddToDefaultView = $true
    )
    $existing = Get-PnPField -List $ListTitle -Identity $InternalName -ErrorAction SilentlyContinue
    if ($existing) { return }

    $target = Get-PnPList -Identity $LookupListTitle -ErrorAction Stop
    $listId = $target.Id.Guid
    $req = if ($Required) { "TRUE" } else { "FALSE" }

    $xml = @"
<Field Type="Lookup"
       DisplayName="$DisplayName"
       StaticName="$InternalName"
       Name="$InternalName"
       List="{$listId}"
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
            Ensure-LookupField -ListTitle $List -DisplayName $DisplayName -InternalName $InternalName -LookupListTitle $LookupList -LookupField $LookupField -Required:$Required -AddToDefaultView:$AddToDefaultView
        } else {
            Add-PnPField -List $List -DisplayName $DisplayName -InternalName $InternalName -Type $Type -AddToDefaultView:$AddToDefaultView | Out-Null
        }
    }

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

# ===========================
# Main
# ===========================
try {
    if (-not (Test-Path $configFile)) {
        "# Finance Hub Configuration" | Set-Content $configFile -Encoding UTF8
    }

    Ensure-Module -Name "PnP.PowerShell" -MinVersion "2.0.0"
    Import-Module PnP.PowerShell -ErrorAction Stop

    # Tenant and brand prompts
    $tenantShort = Get-IniValueOrPrompt -Path $configFile -Section "Tenant" -Key "ShortName" -Prompt "Tenant short name, for example kempy"
    $tenantId = "$tenantShort.onmicrosoft.com"
    $rootUrl  = "https://$tenantShort.sharepoint.com"
    $adminUrl = "https://$tenantShort-admin.sharepoint.com"
    Set-IniValue -Path $configFile -Section "Tenant" -Key "TenantId" -Value $tenantId
    Set-IniValue -Path $configFile -Section "Tenant" -Key "RootUrl" -Value $rootUrl
    Set-IniValue -Path $configFile -Section "Tenant" -Key "AdminUrl" -Value $adminUrl

    $companyDisplay = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "CompanyDisplay" -Prompt "Company display, for example AK" -Default "AK"
    $product = Get-IniValueOrPrompt -Path $configFile -Section "Brand" -Key "Product" -Prompt "Hub type, LedgerHub or FinanceHub" -Default "LedgerHub"
    if ($product -notin @("LedgerHub","FinanceHub")) { $product = "LedgerHub"; Set-IniValue -Path $configFile -Section "Brand" -Key "Product" -Value $product }

    $defaultSiteTitle = "$companyDisplay $product"
    $siteTitle = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteTitle" -Prompt "SharePoint Site Title" -Default $defaultSiteTitle

    $defaultAliasNoDashes = (($companyDisplay + $product) -replace '\s+','').ToLowerInvariant()
    $siteAlias = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "SiteAlias" -Prompt "Site URL name, /sites/<name>" -Default $defaultAliasNoDashes
    $siteUrl = "$rootUrl/sites/$siteAlias"
    Set-IniValue -Path $configFile -Section "SharePoint" -Key "SiteUrl" -Value $siteUrl

    $ownerUpn = Get-IniValueOrPrompt -Path $configFile -Section "SharePoint" -Key "OwnerUpn" -Prompt "Site Owner UPN, only needed if the site will be created" -AllowBlank

    # App registration
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
        Write-Host "`n--- App Registration and Certificate ---`n" -ForegroundColor Cyan

        $match = $false
        do {
            $certPassword = Read-Host -AsSecureString "Enter a password for the certificate, keep it safe"
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
                    Write-Warning "Passwords do not match, case sensitive. Please try again."
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
        Read-Host "After granting consent in the browser, press Enter to continue"
        Pause-ForPropagation -Seconds 45 -Message "Waiting for app permissions"

        $certPwdToUse = $certPassword
    }

    if (-not $certPwdToUse) {
        $certPwdToUse = Read-Host -AsSecureString "Enter the PFX password, needed to connect to SharePoint"
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

    # Create lists and libraries
    Ensure-List -Title "Customers" -Template "GenericList" -EnableAttachments:$false -Url "Customers" -OnQuickLaunch
    Ensure-List -Title "Suppliers" -Template "GenericList" -EnableAttachments:$false -Url "Suppliers" -OnQuickLaunch
    Ensure-List -Title "Ledger"    -Template "GenericList" -EnableAttachments:$false -Url "Ledger" -OnQuickLaunch
    Ensure-List -Title "Invoices"  -Template "DocumentLibrary" -Url "Invoices" -OnQuickLaunch
    Ensure-List -Title "Receipts"  -Template "DocumentLibrary" -Url "Receipts" -OnQuickLaunch

    # Customers fields
    Set-PnPField -List "Customers" -Identity "Title" -Values @{Title="Customer Name"} | Out-Null
    Ensure-Field -List "Customers" -DisplayName "Customer Name" -InternalName "Title" -Type Text -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Customers" -DisplayName "Customer Code" -InternalName "CustomerCode" -Type Text -Required:$true -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Customers" -DisplayName "Address" -InternalName "Address" -Type Note
    Ensure-Field -List "Customers" -DisplayName "Contact Name" -InternalName "ContactName" -Type Text
    Ensure-Field -List "Customers" -DisplayName "Email" -InternalName "Email" -Type Text

    # Suppliers fields
    Set-PnPField -List "Suppliers" -Identity "Title" -Values @{Title="Supplier Name"} | Out-Null
    Ensure-Field -List "Suppliers" -DisplayName "Supplier Name" -InternalName "Title" -Type Text -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Suppliers" -DisplayName "Supplier Code" -InternalName "SupplierCode" -Type Text -Required:$true -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Suppliers" -DisplayName "Category" -InternalName "SupplierCategory" -Type Choice -Choices @("Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Suppliers" -DisplayName "Default VAT Rate" -InternalName "DefaultVATRate" -Type Number
    Ensure-Field -List "Suppliers" -DisplayName "Email" -InternalName "SupplierEmail" -Type Text

    # Ledger fields, full spec
    Set-PnPField -List "Ledger" -Identity "Title" -Values @{Title="Invoice or Expense Number"} | Out-Null
    Ensure-Field -List "Ledger" -DisplayName "Entry Type" -InternalName "EntryType" -Type Choice -Choices @("Income","Expense") -Required:$true
    Ensure-Field -List "Ledger" -DisplayName "Entry Date" -InternalName "EntryDate" -Type DateTime -Required:$true

    # Lookups (version-safe)
    Ensure-Field -List "Ledger" -DisplayName "Customer" -InternalName "Customer" -Type Lookup -LookupList "Customers" -LookupField "Title"
    Ensure-Field -List "Ledger" -DisplayName "Supplier" -InternalName "Supplier" -Type Lookup -LookupList "Suppliers" -LookupField "Title"

    Ensure-Field -List "Ledger" -DisplayName "Supplier Free Text" -InternalName "SupplierFreeText" -Type Text
    Ensure-Field -List "Ledger" -DisplayName "Customer PO or Ref" -InternalName "CustomerPORef" -Type Text
    Ensure-Field -List "Ledger" -DisplayName "Category" -InternalName "Category" -Type Choice -Choices @("Sales","Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")

    # VAT controls
    Ensure-Field -List "Ledger" -DisplayName "VAT Applicability" -InternalName "VATApplicability" -Type Choice -Choices @("Standard","Reduced","Zero-rated","Exempt","Outside Scope")
    Ensure-Field -List "Ledger" -DisplayName "VAT Included" -InternalName "VATIncluded" -Type Boolean
    Set-PnPField -List "Ledger" -Identity "VATIncluded" -Values @{ DefaultValue = "1" } | Out-Null   # default Yes
    Ensure-Field -List "Ledger" -DisplayName "VAT Rate" -InternalName "VATRate" -Type Number
    Set-PnPField -List "Ledger" -Identity "VATRate" -Values @{ DefaultValue = "20" } | Out-Null

    Ensure-Field -List "Ledger" -DisplayName "Amount Net" -InternalName "AmountNet" -Type Currency
    Ensure-Field -List "Ledger" -DisplayName "VAT Amount" -InternalName "VATAmount" -Type Currency
    Ensure-Field -List "Ledger" -DisplayName "Amount Gross" -InternalName "AmountGross" -Type Currency

    Ensure-Field -List "Ledger" -DisplayName "Payment Method" -InternalName "PaymentMethod" -Type Choice -Choices @("Bank","Card","Cash","DLA","Other")
    Ensure-Field -List "Ledger" -DisplayName "Paid" -InternalName "Paid" -Type Boolean
    Ensure-Field -List "Ledger" -DisplayName "Date Paid" -InternalName "DatePaid" -Type DateTime
    Ensure-Field -List "Ledger" -DisplayName "Payment Reference" -InternalName "PaymentReference" -Type Text

    Ensure-Field -List "Ledger" -DisplayName "Tax Year" -InternalName "TaxYear" -Type Text
    Ensure-Field -List "Ledger" -DisplayName "Financial Year" -InternalName "FinancialYear" -Type Text

    Ensure-Field -List "Ledger" -DisplayName "DLA" -InternalName "IsDLA" -Type Boolean
    Ensure-Field -List "Ledger" -DisplayName "Related Document" -InternalName "RelatedDocument" -Type URL
    Ensure-Field -List "Ledger" -DisplayName "Notes" -InternalName "Notes" -Type Note

    # Views for Ledger
    Ensure-View -List "Ledger" -Title "Income" -Fields @("Title","EntryDate","Customer","CustomerPORef","VATApplicability","AmountNet","VATAmount","AmountGross","Paid","RelatedDocument","TaxYear","FinancialYear") -Query '<Where><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Income</Value></Eq></Where>'
    Ensure-View -List "Ledger" -Title "Expenses" -Fields @("Title","EntryDate","Supplier","VATApplicability","Category","AmountNet","VATAmount","AmountGross","Paid","RelatedDocument","TaxYear","FinancialYear") -Query '<Where><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Expense</Value></Eq></Where>'
    Ensure-View -List "Ledger" -Title "Unpaid Invoices" -Fields @("Title","EntryDate","Customer","AmountGross","Paid","DatePaid","RelatedDocument") -Query '<Where><And><Eq><FieldRef Name="EntryType"/><Value Type="Choice">Income</Value></Eq><Eq><FieldRef Name="Paid"/><Value Type="Integer">0</Value></Eq></And></Where>'

    # Invoices library metadata
    Ensure-Field -List "Invoices" -DisplayName "Invoice Number" -InternalName "InvoiceNumber" -Type Text -Indexed:$true -EnforceUnique:$true
    Ensure-Field -List "Invoices" -DisplayName "Customer" -InternalName "InvCustomer" -Type Lookup -LookupList "Customers" -LookupField "Title"
    Ensure-Field -List "Invoices" -DisplayName "PO Reference" -InternalName "POReference" -Type Text
    Ensure-Field -List "Invoices" -DisplayName "Date Issued" -InternalName "InvDateIssued" -Type DateTime
    Ensure-Field -List "Invoices" -DisplayName "Date Paid" -InternalName "InvDatePaid" -Type DateTime
    Ensure-Field -List "Invoices" -DisplayName "Status" -InternalName "InvStatus" -Type Choice -Choices @("Draft","Issued","Paid","Overdue")
    Ensure-Field -List "Invoices" -DisplayName "Amount Net" -InternalName "InvAmountNet" -Type Currency
    Ensure-Field -List "Invoices" -DisplayName "VAT Amount" -InternalName "InvVATAmount" -Type Currency
    Ensure-Field -List "Invoices" -DisplayName "Amount Gross" -InternalName "InvAmountGross" -Type Currency
    Ensure-Field -List "Invoices" -DisplayName "Tax Year" -InternalName "TaxYear" -Type Text
    Ensure-Field -List "Invoices" -DisplayName "Financial Year" -InternalName "FinancialYear" -Type Text

    Ensure-View -List "Invoices" -Title "All Invoices" -Fields @("FileLeafRef","InvoiceNumber","InvCustomer","POReference","InvDateIssued","InvStatus","InvAmountNet","InvVATAmount","InvAmountGross","TaxYear","FinancialYear")

    # Receipts library metadata
    Ensure-Field -List "Receipts" -DisplayName "Supplier" -InternalName "RecSupplier" -Type Lookup -LookupList "Suppliers" -LookupField "Title"
    Ensure-Field -List "Receipts" -DisplayName "Category" -InternalName "RecCategory" -Type Choice -Choices @("Equipment","Software","Cloud Services","Fuel","Travel","Insurance","Professional Fees","Other")
    Ensure-Field -List "Receipts" -DisplayName "Date" -InternalName "RecDate" -Type DateTime
    Ensure-Field -List "Receipts" -DisplayName "Amount Gross" -InternalName "RecAmountGross" -Type Currency
    Ensure-Field -List "Receipts" -DisplayName "Ledger Item ID" -InternalName "RecLedgerId" -Type Number
    Ensure-Field -List "Receipts" -DisplayName "Tax Year" -InternalName "TaxYear" -Type Text
    Ensure-Field -List "Receipts" -DisplayName "Financial Year" -InternalName "FinancialYear" -Type Text

    Ensure-View -List "Receipts" -Title "All Receipts" -Fields @("FileLeafRef","RecSupplier","RecDate","RecAmountGross","RecLedgerId","RecCategory","TaxYear","FinancialYear")

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