# Add Missing Columns to Company Settings List
# This adds: SmtpServer, SmtpPort, HasAuthorizedOfficer

$siteUrl = "https://kempy.sharepoint.com/sites/AKFinancehubV2"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Adding Missing Columns to Company Settings List" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

try {
    # Connect to SharePoint using PnP Management Shell (built-in app)
    Write-Host "Connecting to SharePoint site..." -ForegroundColor Yellow
    Write-Host "This will open a browser window for authentication..." -ForegroundColor Gray
    Connect-PnPOnline -Url $siteUrl -UseWebLogin -ErrorAction Stop
    Write-Host "✓ Connected successfully!" -ForegroundColor Green
    Write-Host ""

    # Function to add field if it doesn't exist
    function Add-FieldIfMissing {
        param(
            [string]$ListName,
            [string]$DisplayName,
            [string]$InternalName,
            [string]$Type
        )
        
        try {
            $field = Get-PnPField -List $ListName -Identity $InternalName -ErrorAction SilentlyContinue
            if ($field) {
                Write-Host "  ℹ  $DisplayName ($InternalName) - Already exists" -ForegroundColor Gray
            } else {
                Add-PnPField -List $ListName -DisplayName $DisplayName -InternalName $InternalName -Type $Type -AddToDefaultView | Out-Null
                Write-Host "  ✓  $DisplayName ($InternalName) - CREATED" -ForegroundColor Green
            }
        }
        catch {
            Write-Host "  ✗  $DisplayName ($InternalName) - ERROR: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Write-Host "Adding missing columns to 'Company Settings' list..." -ForegroundColor Yellow
    Write-Host ""

    # Add the 3 missing columns
    Add-FieldIfMissing -ListName "Company Settings" -DisplayName "SMTP Server" -InternalName "SmtpServer" -Type "Text"
    Add-FieldIfMissing -ListName "Company Settings" -DisplayName "SMTP Port" -InternalName "SmtpPort" -Type "Number"
    Add-FieldIfMissing -ListName "Company Settings" -DisplayName "Has Authorized Officer" -InternalName "HasAuthorizedOfficer" -Type "Boolean"

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "  ✓ Column creation completed!" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Go to https://hub.kemponline.co.uk" -ForegroundColor White
    Write-Host "2. Navigate to Settings → SMTP Configuration" -ForegroundColor White
    Write-Host "3. Enter:" -ForegroundColor White
    Write-Host "   - SMTP Server: mail.smtp2go.com" -ForegroundColor Gray
    Write-Host "   - SMTP Port: 2525" -ForegroundColor Gray
    Write-Host "   - SMTP From Address: noreply@andykemp.com" -ForegroundColor Gray
    Write-Host "   - SMTP Username: <your smtp2go username>" -ForegroundColor Gray
    Write-Host "   - SMTP Password: <your smtp2go password>" -ForegroundColor Gray
    Write-Host "4. Click 'Send Test Email' to verify" -ForegroundColor White
    Write-Host "5. Navigate to Digital Signatures tab" -ForegroundColor White
    Write-Host "6. Draw signatures and click 'Save Signature'" -ForegroundColor White
    Write-Host "7. Click 'Save Settings' at the bottom" -ForegroundColor White
    Write-Host ""

    Disconnect-PnPOnline
}
catch {
    Write-Host ""
    Write-Host "✗ ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "If you see authentication errors:" -ForegroundColor Yellow
    Write-Host "1. Make sure you have PnP.PowerShell installed:" -ForegroundColor White
    Write-Host "   Install-Module -Name PnP.PowerShell -Scope CurrentUser" -ForegroundColor Gray
    Write-Host "2. If it prompts for app registration, just press Enter to use defaults" -ForegroundColor White
    Write-Host ""
    exit 1
}
