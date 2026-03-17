# Add missing columns to Company Settings SharePoint list using REST API
# This script uses CLI for Microsoft 365 which authenticates interactively

# SharePoint site URL
$siteUrl = "https://kempy.sharepoint.com/sites/AKFinancehubV2"
$listName = "Company Settings"

Write-Host "Connecting to SharePoint site using CLI for Microsoft 365..." -ForegroundColor Cyan

try {
    # Login to Microsoft 365 (will open browser for authentication)
    Write-Host "Opening browser for authentication..." -ForegroundColor Yellow
    m365 login --authType browser
    
    Write-Host "Connected successfully!" -ForegroundColor Green
    Write-Host "Connected successfully!" -ForegroundColor Green
    
    # Add DirectorSignature column (Multiple lines of text for base64 signature)
    Write-Host "`nAdding DirectorSignature column..." -ForegroundColor Yellow
    try {
        m365 spo field add --webUrl $siteUrl --listTitle $listName --title "DirectorSignature" --name "DirectorSignature" --type Note
        Write-Host "  ✓ DirectorSignature column added" -ForegroundColor Green
    }
    catch {
        Write-Host "  ℹ DirectorSignature may already exist or error occurred" -ForegroundColor Gray
    }
    
    # Add HasAuthorizedOfficer column (Yes/No boolean)
    Write-Host "Adding HasAuthorizedOfficer column..." -ForegroundColor Yellow
    try {
        m365 spo field add --webUrl $siteUrl --listTitle $listName --title "HasAuthorizedOfficer" --name "HasAuthorizedOfficer" --type Boolean
        Write-Host "  ✓ HasAuthorizedOfficer column added" -ForegroundColor Green
    }
    catch {
        Write-Host "  ℹ HasAuthorizedOfficer may already exist or error occurred" -ForegroundColor Gray
    }
    
    # Add AuthorizedOfficerName column (Single line of text)
    Write-Host "Adding AuthorizedOfficerName column..." -ForegroundColor Yellow
    try {
        m365 spo field add --webUrl $siteUrl --listTitle $listName --title "AuthorizedOfficerName" --name "AuthorizedOfficerName" --type Text
        Write-Host "  ✓ AuthorizedOfficerName column added" -ForegroundColor Green
    }
    catch {
        Write-Host "  ℹ AuthorizedOfficerName may already exist or error occurred" -ForegroundColor Gray
    }
    
    # Add AuthorizedOfficerSignature column (Multiple lines of text for base64 signature)
    Write-Host "Adding AuthorizedOfficerSignature column..." -ForegroundColor Yellow
    try {
        m365 spo field add --webUrl $siteUrl --listTitle $listName --title "AuthorizedOfficerSignature" --name "AuthorizedOfficerSignature" --type Note
        Write-Host "  ✓ AuthorizedOfficerSignature column added" -ForegroundColor Green
    }
    catch {
        Write-Host "  ℹ AuthorizedOfficerSignature may already exist or error occurred" -ForegroundColor Gray
    }
    
    # Add SmtpServer column (Single line of text)
    Write-Host "Adding SmtpServer column..." -ForegroundColor Yellow
    try {
        m365 spo field add --webUrl $siteUrl --listTitle $listName --title "SmtpServer" --name "SmtpServer" --type Text
        Write-Host "  ✓ SmtpServer column added" -ForegroundColor Green
    }
    catch {
        Write-Host "  ℹ SmtpServer may already exist or error occurred" -ForegroundColor Gray
    }
    
    # Add SmtpPort column (Number)
    Write-Host "Adding SmtpPort column..." -ForegroundColor Yellow
    try {
        m365 spo field add --webUrl $siteUrl --listTitle $listName --title "SmtpPort" --name "SmtpPort" --type Number
        Write-Host "  ✓ SmtpPort column added" -ForegroundColor Green
    }
    catch {
        Write-Host "  ℹ SmtpPort may already exist or error occurred" -ForegroundColor Gray
    }
    
    Write-Host "`n✓ Column creation script completed!" -ForegroundColor Green
    
    # Logout
    m365 logout
}
catch {
    Write-Host "`n✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}
