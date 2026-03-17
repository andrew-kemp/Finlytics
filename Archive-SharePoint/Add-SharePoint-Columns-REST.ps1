# Add SharePoint columns using REST API with device code authentication
$siteUrl = "https://kempy.sharepoint.com/sites/AKFinancehubV2"
$listName = "Company Settings"

Write-Host "Adding columns to SharePoint Company Settings list..." -ForegroundColor Cyan

# Login using device code (will open browser)
Write-Host "Logging in to Azure AD..." -ForegroundColor Yellow
az login --use-device-code

# Get access token for SharePoint
Write-Host "Getting access token for SharePoint..." -ForegroundColor Yellow
$token = az account get-access-token --resource "https://kempy.sharepoint.com" --query accessToken -o tsv

$headers = @{
    "Authorization" = "Bearer $token"
    "Accept" = "application/json;odata=verbose"
    "Content-Type" = "application/json;odata=verbose"
}

# Get list ID
$listEndpoint = "$siteUrl/_api/web/lists/getbytitle('$listName')"
try {
    $listResponse = Invoke-RestMethod -Uri $listEndpoint -Headers $headers -Method Get
    Write-Host "✓ Found Company Settings list" -ForegroundColor Green
} catch {
    Write-Host "✗ Error: Could not find Company Settings list" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# Function to add a column
function Add-SPColumn {
    param(
        [string]$FieldName,
        [string]$DisplayName,
        [string]$FieldType,
        [hashtable]$AdditionalParams = @{}
    )
    
    Write-Host "`nAdding column: $DisplayName..." -ForegroundColor Yellow
    
    $fieldXml = "<Field DisplayName='$DisplayName' Name='$FieldName' Type='$FieldType'"
    
    foreach ($key in $AdditionalParams.Keys) {
        $fieldXml += " $key='$($AdditionalParams[$key])'"
    }
    
    $fieldXml += " />"
    
    $body = @{
        "__metadata" = @{ "type" = "SP.Field" }
        "parameters" = @{
            "__metadata" = @{ "type" = "SP.FieldCreationInformation" }
            "FieldTypeKind" = switch ($FieldType) {
                "Text" { 2 }
                "Note" { 3 }
                "Number" { 9 }
                "Boolean" { 8 }
                default { 2 }
            }
            "Title" = $DisplayName
        }
    } | ConvertTo-Json -Depth 10
    
    $addFieldEndpoint = "$siteUrl/_api/web/lists/getbytitle('$listName')/fields"
    
    try {
        $response = Invoke-RestMethod -Uri $addFieldEndpoint -Headers $headers -Method Post -Body $body
        Write-Host "  ✓ Column '$DisplayName' added successfully" -ForegroundColor Green
        return $true
    } catch {
        if ($_.Exception.Message -like "*already exists*" -or $_.Exception.Message -like "*duplicate*") {
            Write-Host "  ℹ Column '$DisplayName' already exists, skipping" -ForegroundColor Gray
            return $true
        } else {
            Write-Host "  ✗ Error adding column: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
}

# Add columns
Write-Host "`n=== Adding Signature Columns ===" -ForegroundColor Cyan
Add-SPColumn -FieldName "DirectorSignature" -DisplayName "DirectorSignature" -FieldType "Note"
Add-SPColumn -FieldName "HasAuthorizedOfficer" -DisplayName "HasAuthorizedOfficer" -FieldType "Boolean"
Add-SPColumn -FieldName "AuthorizedOfficerName" -DisplayName "AuthorizedOfficerName" -FieldType "Text"
Add-SPColumn -FieldName "AuthorizedOfficerSignature" -DisplayName "AuthorizedOfficerSignature" -FieldType "Note"

Write-Host "`n=== Adding SMTP Configuration Columns ===" -ForegroundColor Cyan
Add-SPColumn -FieldName "SmtpServer" -DisplayName "SmtpServer" -FieldType "Text"
Add-SPColumn -FieldName "SmtpPort" -DisplayName "SmtpPort" -FieldType "Number"

Write-Host "`n✓ All columns have been processed!" -ForegroundColor Green
Write-Host "`nYou can now:" -ForegroundColor Cyan
Write-Host "1. Go to https://hub.kemponline.co.uk" -ForegroundColor White
Write-Host "2. Open Settings → SMTP Configuration" -ForegroundColor White
Write-Host "3. Fill in SMTP server, port, username, from address" -ForegroundColor White
Write-Host "4. Go to Digital Signatures tab" -ForegroundColor White
Write-Host "5. Draw signatures and click Save Settings" -ForegroundColor White
Write-Host "6. Refresh page - signatures should persist!" -ForegroundColor White
