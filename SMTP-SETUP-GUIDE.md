# SMTP Configuration Setup Guide

## Overview
The Settings page allows you to configure SMTP2Go email settings for sending certificates and notifications. The configuration is split between SharePoint (Company Settings list) and Azure Key Vault for security.

## Step 1: Add SMTP Fields to Company Settings List in SharePoint

1. Go to your SharePoint site: https://kempy.sharepoint.com/sites/AKFinancehubV2
2. Navigate to **Company Settings** list
3. Click **Settings** → **List settings**
4. Under **Columns**, click **Create column**

### Add SmtpFromAddress Column:
- Column name: `SmtpFromAddress`
- Type: **Single line of text**
- Required: No
- Click **OK**

### Add SmtpUsername Column:
- Column name: `SmtpUsername`
- Type: **Single line of text**
- Required: No
- Click **OK**

## Step 2: Configure Azure Key Vault for SMTP Password

### Option A: Using Azure Portal
1. Go to Azure Portal: https://portal.azure.com
2. Navigate to your Key Vault (search for "Key vault" in resources)
3. Click on **Secrets** in the left menu
4. Click **+ Generate/Import**
5. Configure the secret:
   - **Upload options**: Manual
   - **Name**: `SmtpPassword`
   - **Value**: Your SMTP2Go password
   - **Content type**: (leave blank)
   - **Activation date**: (leave blank)
   - **Expiration date**: (leave blank)
   - **Enabled**: Yes
6. Click **Create**

### Option B: Using Azure CLI
```powershell
# Set your Key Vault name
$keyVaultName = "your-keyvault-name"

# Set the SMTP password
$smtpPassword = "your-smtp2go-password"

# Create the secret
az keyvault secret set --vault-name $keyVaultName --name "SmtpPassword" --value $smtpPassword
```

## Step 3: Ensure Function App Has Access to Key Vault

The Function App uses Managed Identity to access Key Vault. Verify access is configured:

### Using Azure Portal:
1. Go to your Key Vault
2. Click **Access policies** in the left menu
3. Verify that your Function App (func-financehub-2669) has an access policy
4. If not, click **+ Add Access Policy**:
   - **Secret permissions**: Get, List
   - **Select principal**: Search for "func-financehub-2669"
   - Click **Add**
   - Click **Save**

### Using Azure CLI:
```powershell
# Get the Function App's managed identity
$functionAppName = "func-financehub-2669"
$resourceGroup = "rg-financehub-prod"
$keyVaultName = "your-keyvault-name"

# Get the principal ID
$principalId = az functionapp identity show --name $functionAppName --resource-group $resourceGroup --query principalId -o tsv

# Grant access
az keyvault set-policy --name $keyVaultName --object-id $principalId --secret-permissions get list
```

## Step 4: Configure SMTP Settings in the Application

1. Sign in to your Finance Hub: https://hub.kemponline.co.uk
2. Click **⚙️ Settings** in the navigation menu
3. Enter your SMTP configuration:
   - **From Email Address**: The email address emails will be sent from (e.g., noreply@kemponline.co.uk)
   - **SMTP2Go Username**: Your SMTP2Go account username
4. Click **💾 Save Settings**

## Step 5: Get SMTP2Go Credentials

If you don't have SMTP2Go credentials yet:

1. Sign up for SMTP2Go: https://www.smtp2go.com/
2. After creating your account, go to **Settings** → **Users**
3. Create a new SMTP user or use existing credentials
4. Note down:
   - Username (for Settings page)
   - Password (for Azure Key Vault)
   - Server: mail.smtp2go.com
   - Port: 2525, 587, or 8025 (TLS) / 465 (SSL)

## Verification

After completing all steps:
1. Check that the Settings page displays your configuration correctly
2. The password will never be shown in the UI (it's stored securely in Key Vault)
3. When email functionality is implemented, it will automatically retrieve the password from Key Vault

## Security Notes

- ✅ SMTP password is stored in Azure Key Vault, never in SharePoint or code
- ✅ Function App uses Managed Identity to access Key Vault (no connection strings needed)
- ✅ From Address and Username are stored in SharePoint for easy management
- ✅ Only authorized users can access the Settings page (requires sign-in)

## Troubleshooting

### "Failed to load settings"
- Check that SmtpFromAddress and SmtpUsername columns exist in Company Settings list
- Verify you have read access to the Company Settings list

### "Failed to save settings"
- Check that you have edit permissions on the Company Settings list
- Verify the columns are not marked as Required if they're empty

### Email sending fails (when implemented)
- Verify the SmtpPassword secret exists in Key Vault with the exact name
- Check that the Function App has Get/List permissions on Key Vault secrets
- Verify SMTP2Go credentials are correct
