# Update Currency Symbol in Company Settings
# This script updates the Currency Symbol field in the Company Settings SharePoint list

param(
    [string]$SiteUrl = "https://kempy.sharepoint.com/sites/AKFinancehubV2",
    [string]$CurrencySymbol = "£"
)

Write-Host "Connecting to SharePoint..." -ForegroundColor Cyan
Connect-PnPOnline -Url $SiteUrl -Interactive

Write-Host "Getting Company Settings item..." -ForegroundColor Cyan
$item = Get-PnPListItem -List "Company Settings" -Query "<View><Query><Where><Eq><FieldRef Name='Title'/><Value Type='Text'>Default</Value></Eq></Where></Query></View>"

if ($item) {
    Write-Host "Updating Currency Symbol to '$CurrencySymbol'..." -ForegroundColor Cyan
    Set-PnPListItem -List "Company Settings" -Identity $item.Id -Values @{
        "CurrencySymbol" = $CurrencySymbol
    }
    Write-Host "✓ Currency Symbol updated successfully!" -ForegroundColor Green
    
    # Display current settings
    Write-Host "`nCurrent Company Settings:" -ForegroundColor Yellow
    $updatedItem = Get-PnPListItem -List "Company Settings" -Id $item.Id
    Write-Host "  Company Name: $($updatedItem['CompanyName'])"
    Write-Host "  Base Currency: $($updatedItem['BaseCurrency'])"
    Write-Host "  Currency Symbol: $($updatedItem['CurrencySymbol'])"
}
else {
    Write-Host "✗ Company Settings item not found!" -ForegroundColor Red
}

Disconnect-PnPOnline
