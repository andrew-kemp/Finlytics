# Check Company Settings in SharePoint
param(
    [string]$ConfigFile = ".\finance-hub-config.ini"
)

function Get-IniValue {
    param([string]$Path, [string]$Section, [string]$Key)
    $content = Get-Content $Path -Raw
    if ($content -match "(?ms)\[$Section\][^\[]*?$Key\s*=\s*(.+?)(\r?\n|$)") {
        return $matches[1].Trim()
    }
    return $null
}

# Read config
$tenantId = Get-IniValue -Path $ConfigFile -Section "Azure" -Key "TenantId"
$siteUrl = Get-IniValue -Path $ConfigFile -Section "SharePoint" -Key "SiteUrl"
$certThumbprint = Get-IniValue -Path $ConfigFile -Section "App" -Key "Thumbprint"
$appId = Get-IniValue -Path $ConfigFile -Section "App" -Key "ClientId"

Write-Host "Connecting to SharePoint Online..." -ForegroundColor Cyan
Write-Host "  Tenant: $tenantId"
Write-Host "  Site: $siteUrl"
Write-Host "  App ID: $appId"
Write-Host "  Cert: $certThumbprint"

# Connect using certificate
Connect-PnPOnline -Url $siteUrl -ClientId $appId -Tenant $tenantId -Thumbprint $certThumbprint

Write-Host "`nChecking Company Settings list..." -ForegroundColor Cyan

# Get the list
$list = Get-PnPList -Identity "Company Settings" -Includes Fields

Write-Host "`n=== List Fields ===" -ForegroundColor Yellow
$list.Fields | Where-Object { -not $_.Hidden -and $_.InternalName -notlike "_*" } | 
    Select-Object Title, InternalName, TypeAsString | 
    Format-Table -AutoSize

Write-Host "`n=== Looking for Inception Date field ===" -ForegroundColor Yellow
$inceptionField = $list.Fields | Where-Object { $_.InternalName -like "*Inception*" -or $_.Title -like "*Inception*" }
if ($inceptionField) {
    $inceptionField | Select-Object Title, InternalName, TypeAsString, Required, DefaultValue | Format-List
} else {
    Write-Host "NO INCEPTION DATE FIELD FOUND!" -ForegroundColor Red
}

Write-Host "`n=== Looking for FY fields ===" -ForegroundColor Yellow
$fyFields = $list.Fields | Where-Object { $_.InternalName -like "*FY*" -or $_.Title -like "*FY*" }
if ($fyFields) {
    $fyFields | Select-Object Title, InternalName, TypeAsString | Format-Table -AutoSize
} else {
    Write-Host "NO FY FIELDS FOUND!" -ForegroundColor Red
}

Write-Host "`n=== Current Company Settings Item ===" -ForegroundColor Yellow
$item = Get-PnPListItem -List "Company Settings" -Query "<View><RowLimit>1</RowLimit></View>"

if ($item) {
    Write-Host "Item ID: $($item.Id)"
    Write-Host "`nAll field values:"
    $item.FieldValues.GetEnumerator() | Where-Object { $_.Key -notlike "_*" -and $_.Key -notlike "GUID" } |
        Sort-Object Key | 
        ForEach-Object {
            $value = if ($_.Value) { $_.Value } else { "(null)" }
            Write-Host "  $($_.Key) = $value"
        }
    
    Write-Host "`nSpecific fields we care about:" -ForegroundColor Cyan
    Write-Host "  BusinessName: $($item['BusinessName'])"
    Write-Host "  CompanyInceptionDate: $($item['CompanyInceptionDate'])"
    Write-Host "  FYStartMonth: $($item['FYStartMonth'])"
    Write-Host "  FYStartDay: $($item['FYStartDay'])"
} else {
    Write-Host "NO ITEMS FOUND!" -ForegroundColor Red
}

Disconnect-PnPOnline
