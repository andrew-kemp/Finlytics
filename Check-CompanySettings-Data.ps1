# Check what data exists in old vs new CompanySettings fields
$apiUrl = "https://financehub-func-kemponline.azurewebsites.net/api/GetCompanySettings"
$data = Invoke-RestMethod -Uri $apiUrl -Method Get

Write-Host "`n=== COMPANY SETTINGS DATA CHECK ===" -ForegroundColor Cyan
Write-Host "`nChecking OLD vs NEW field names for data..." -ForegroundColor Yellow

$fieldPairs = @(
    @{Old='address'; New='companyAddress'; Label='Address'}
    @{Old='phoneNumber'; New='companyPhone'; Label='Phone'}
    @{Old='email'; New='companyEmail'; Label='Email'}
    @{Old='vatNumber'; New='vatRegistrationNumber'; Label='VAT Number'}
    @{Old='accountNumber'; New='bankAccountNumber'; Label='Bank Account'}
    @{Old='sortCode'; New='bankSortCode'; Label='Sort Code'}
    @{Old='footerText'; New='invoiceFooterText'; Label='Footer Text'}
    @{Old='paymentTerms'; New='invoiceTermsDays'; Label='Payment Terms'}
)

$hasOldData = $false
$hasNewData = $false

foreach ($pair in $fieldPairs) {
    $oldValue = $data.($pair.Old)
    $newValue = $data.($pair.New)
    
    if ($oldValue -or $newValue) {
        Write-Host "`n$($pair.Label):" -ForegroundColor White
        Write-Host "  OLD field ($($pair.Old)): '$oldValue'" -ForegroundColor $(if($oldValue){"Green"}else{"Red"})
        Write-Host "  NEW field ($($pair.New)): '$newValue'" -ForegroundColor $(if($newValue){"Green"}else{"Red"})
        
        if ($oldValue -and !$newValue) {
            $hasOldData = $true
            Write-Host "  ⚠️  DATA EXISTS IN OLD FIELD BUT NOT NEW!" -ForegroundColor Red
        }
        elseif ($newValue -and !$oldValue) {
            $hasNewData = $true
        }
    }
}

Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
if ($hasOldData) {
    Write-Host "❌ PROBLEM FOUND: Data exists in OLD field names but not NEW field names!" -ForegroundColor Red
    Write-Host "   Frontend is looking for NEW field names, so data won't display." -ForegroundColor Yellow
    Write-Host "   Need to migrate data from old fields to new fields." -ForegroundColor Yellow
}
elseif (!$hasOldData -and !$hasNewData) {
    Write-Host "ℹ️  No data found in either old or new fields - database is empty for these fields." -ForegroundColor Gray
}
else {
    Write-Host "✅ Data is in the correct NEW field names." -ForegroundColor Green
}

Write-Host "`nFields with data:" -ForegroundColor Cyan
$data.PSObject.Properties | Where-Object { $_.Value } | ForEach-Object {
    Write-Host "  - $($_.Name): $($_.Value)" -ForegroundColor Gray
}
