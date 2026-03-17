# Grant Email Permissions to Function App Managed Identity
# Run this script as an Azure AD Global Administrator or Exchange Administrator

# Connect to Exchange Online PowerShell
Connect-ExchangeOnline

# Get the Function App's Managed Identity Object ID
$functionAppName = "func-financehub-2669"
$resourceGroup = "rg-financehub-prod"

# Get the Managed Identity Object ID
$managedIdentityObjectId = (Get-AzFunctionApp -ResourceGroupName $resourceGroup -Name $functionAppName).IdentityPrincipalId

Write-Host "Function App Managed Identity Object ID: $managedIdentityObjectId" -ForegroundColor Green

# Grant SendAs permission for the mailbox
$mailbox = "invoices@andykemp.com"

Write-Host "Granting SendAs permission for $mailbox to Managed Identity..." -ForegroundColor Yellow

Add-RecipientPermission -Identity $mailbox -Trustee $managedIdentityObjectId -AccessRights SendAs -Confirm:$false

Write-Host "✓ SendAs permission granted successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Note: It may take a few minutes for the permissions to propagate." -ForegroundColor Yellow
Write-Host "After granting permissions, test by sending an invoice email from the portal." -ForegroundColor Cyan
