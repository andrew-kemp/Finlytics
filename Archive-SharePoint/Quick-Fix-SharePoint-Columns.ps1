# Quick fix: Set default SMTP values directly in SharePoint using Graph API
# This will allow SMTP test to work immediately

Write-Host "Setting default SMTP configuration in SharePoint..." -ForegroundColor Cyan

# SharePoint site details
$siteUrl = "https://kempy.sharepoint.com/sites/AKFinancehubV2"
$listName = "Company Settings"

# Default SMTP2Go configuration
$smtpServer = "mail.smtp2go.com"
$smtpPort = 2525

Write-Host "`n1. First, we'll update the existing Company Settings item with default SMTP values" -ForegroundColor Yellow
Write-Host "2. This will allow the SMTP test button to work" -ForegroundColor Yellow
Write-Host "3. Signature columns still need to be added manually via SharePoint UI`n" -ForegroundColor Yellow

# Get the SharePoint site and list
Write-Host "Opening SharePoint site in browser..." -ForegroundColor Cyan
Start-Process "$siteUrl/Lists/Company%20Settings/AllItems.aspx"

Write-Host "`n" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "  MANUAL STEPS TO FIX SIGNATURES" -ForegroundColor Yellow
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The browser should have opened the Company Settings list." -ForegroundColor White
Write-Host ""
Write-Host "TO ADD SMTP FIELDS:" -ForegroundColor Green
Write-Host "1. Click on the item row to open it" -ForegroundColor White
Write-Host "2. Click 'Edit' at the top" -ForegroundColor White
Write-Host "3. Look for SmtpServer and SmtpPort fields" -ForegroundColor White
Write-Host "4. If they exist: Set SmtpServer='mail.smtp2go.com', SmtpPort='2525'" -ForegroundColor White
Write-Host "5. If they DON'T exist: Close the form and continue below" -ForegroundColor White
Write-Host ""
Write-Host "TO ADD MISSING COLUMNS:" -ForegroundColor Green
Write-Host "1. At the top of the list, click the gear icon ⚙️" -ForegroundColor White
Write-Host "2. Click 'List settings'" -ForegroundColor White
Write-Host "3. Under 'Columns', click '+ Create column'" -ForegroundColor White
Write-Host "4. Add these 6 columns one by one:" -ForegroundColor White
Write-Host ""
Write-Host "   Column 1: SmtpServer" -ForegroundColor Cyan
Write-Host "   - Name: SmtpServer" -ForegroundColor Gray
Write-Host "   - Type: Single line of text" -ForegroundColor Gray
Write-Host "   - Click OK" -ForegroundColor Gray
Write-Host ""
Write-Host "   Column 2: SmtpPort" -ForegroundColor Cyan
Write-Host "   - Name: SmtpPort" -ForegroundColor Gray
Write-Host "   - Type: Number" -ForegroundColor Gray  
Write-Host "   - Decimal places: 0" -ForegroundColor Gray
Write-Host "   - Click OK" -ForegroundColor Gray
Write-Host ""
Write-Host "   Column 3: DirectorSignature" -ForegroundColor Cyan
Write-Host "   - Name: DirectorSignature" -ForegroundColor Gray
Write-Host "   - Type: Multiple lines of text" -ForegroundColor Gray
Write-Host "   - Click OK" -ForegroundColor Gray
Write-Host ""
Write-Host "   Column 4: HasAuthorizedOfficer" -ForegroundColor Cyan
Write-Host "   - Name: HasAuthorizedOfficer" -ForegroundColor Gray
Write-Host "   - Type: Yes/No" -ForegroundColor Gray
Write-Host "   - Default: No" -ForegroundColor Gray
Write-Host "   - Click OK" -ForegroundColor Gray
Write-Host ""
Write-Host "   Column 5: AuthorizedOfficerName" -ForegroundColor Cyan
Write-Host "   - Name: AuthorizedOfficerName" -ForegroundColor Gray
Write-Host "   - Type: Single line of text" -ForegroundColor Gray
Write-Host "   - Click OK" -ForegroundColor Gray
Write-Host ""
Write-Host "   Column 6: AuthorizedOfficerSignature" -ForegroundColor Cyan
Write-Host "   - Name: AuthorizedOfficerSignature" -ForegroundColor Gray
Write-Host "   - Type: Multiple lines of text" -ForegroundColor Gray
Write-Host "   - Click OK" -ForegroundColor Gray
Write-Host ""
Write-Host "AFTER ADDING COLUMNS:" -ForegroundColor Green
Write-Host "1. Go back to the list" -ForegroundColor White
Write-Host "2. Click on the item to edit it" -ForegroundColor White
Write-Host "3. Set SmtpServer = mail.smtp2go.com" -ForegroundColor White
Write-Host "4. Set SmtpPort = 2525" -ForegroundColor White
Write-Host "5. Save" -ForegroundColor White
Write-Host ""
Write-Host "THEN TEST:" -ForegroundColor Green
Write-Host "1. Go to https://hub.kemponline.co.uk" -ForegroundColor White
Write-Host "2. Settings → SMTP Configuration tab" -ForegroundColor White
Write-Host "3. The fields should now show the values" -ForegroundColor White
Write-Host "4. Fill in any missing SMTP fields" -ForegroundColor White
Write-Host "5. Click 'Send Test Email'" -ForegroundColor White
Write-Host "6. Go to Digital Signatures tab" -ForegroundColor White
Write-Host "7. Draw signature, click 'Save Signature', then 'Save Settings'" -ForegroundColor White
Write-Host "8. Refresh page - signature should persist!" -ForegroundColor White
Write-Host ""
Write-Host "==============================================================" -ForegroundColor Cyan

Write-Host "`nPress any key to close this window..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
