# Update Currency Symbol via Azure CLI (REST API)
# This uses the logged-in Azure account to update SharePoint via Graph API

$siteUrl = "kempy.sharepoint.com:/sites/AKFinancehubV2"
$listName = "Company Settings"
$currencySymbol = "£"

Write-Host "Getting access token..." -ForegroundColor Cyan
$token = az account get-access-token --resource "https://graph.microsoft.com" --query accessToken -o tsv

Write-Host "Getting site ID..." -ForegroundColor Cyan
$siteResponse = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/sites/$siteUrl" `
    -Headers @{ Authorization = "Bearer $token" } `
    -Method Get

$siteId = $siteResponse.id
Write-Host "  Site ID: $siteId" -ForegroundColor Gray

Write-Host "Getting list ID..." -ForegroundColor Cyan
$listResponse = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists?`$filter=displayName eq '$listName'" `
    -Headers @{ Authorization = "Bearer $token" } `
    -Method Get

$listId = $listResponse.value[0].id
Write-Host "  List ID: $listId" -ForegroundColor Gray

Write-Host "Getting Company Settings items..." -ForegroundColor Cyan
$itemsResponse = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$listId/items?`$expand=fields" `
    -Headers @{ Authorization = "Bearer $token" } `
    -Method Get

$item = $itemsResponse.value | Where-Object { $_.fields.Title -eq "Default" }

if ($item) {
    Write-Host "Updating Currency Symbol to '$currencySymbol'..." -ForegroundColor Cyan
    $updateBody = @{
        fields = @{
            CurrencySymbol = $currencySymbol
        }
    } | ConvertTo-Json
    
    $updateResponse = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$listId/items/$($item.id)" `
        -Headers @{ 
            Authorization = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Method Patch `
        -Body $updateBody
    
    Write-Host "✓ Currency Symbol updated successfully!" -ForegroundColor Green
    Write-Host "`nCurrent Settings:" -ForegroundColor Yellow
    Write-Host "  Company Name: $($updateResponse.fields.CompanyName)"
    Write-Host "  Base Currency: $($updateResponse.fields.BaseCurrency)"
    Write-Host "  Currency Symbol: $($updateResponse.fields.CurrencySymbol)"
}
else {
    Write-Host "✗ Company Settings item not found!" -ForegroundColor Red
}
