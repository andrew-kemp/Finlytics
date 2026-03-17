# Add SharePoint Columns to Company Settings List

## Quick Steps to Add Columns via SharePoint UI

1. **Open SharePoint Site**:
   - Go to https://kempy.sharepoint.com/sites/AKFinancehubV2
   - Navigate to **Company Settings** list

2. **Open List Settings**:
   - Click the gear icon (⚙️) → **List settings**
   - OR: Click **+Add column** at the top of the list

3. **Add the following columns**:

### Signature Fields

**DirectorSignature** (Multiple lines of text):
- Column name: `DirectorSignature`
- Type: **Multiple lines of text**
- More options: Select **Plain text**
- Click **OK**

**HasAuthorizedOfficer** (Yes/No):
- Column name: `HasAuthorizedOfficer`
- Type: **Yes/No**
- Default value: **No**
- Click **OK**

**AuthorizedOfficerName** (Single line of text):
- Column name: `AuthorizedOfficerName`
- Type: **Single line of text**
- Click **OK**

**AuthorizedOfficerSignature** (Multiple lines of text):
- Column name: `AuthorizedOfficerSignature`
- Type: **Multiple lines of text**
- More options: Select **Plain text**
- Click **OK**

### SMTP Configuration Fields

**SmtpServer** (Single line of text):
- Column name: `SmtpServer`
- Type: **Single line of text**
- Click **OK**

**SmtpPort** (Number):
- Column name: `SmtpPort`
- Type: **Number**
- Min/Max: Leave blank (or set 1-65535 for port range)
- Decimal places: **0**
- Default value: **2525** (optional - SMTP2Go default port)
- Click **OK**

## After Adding Columns

1. **Go to Settings Page**: https://hub.kemponline.co.uk
2. Navigate to **Settings** → **SMTP Configuration** tab
3. Enter SMTP configuration:
   - SMTP Server: `mail.smtp2go.com` (or your SMTP server)
   - SMTP Port: `2525` (or your SMTP port - common: 587, 465, 25)
   - SMTP From Address: Your default sending email
   - SMTP Username: Your SMTP username
   - SMTP Password: Your SMTP password (stored in Azure Key Vault)
4. Click **Send Test Email** to verify configuration
5. Navigate to **Digital Signatures** tab
6. Enter Director Name and draw signature
7. Optionally check "Company has an Authorized Officer" and add second signature
8. Click **Save Signature** for each signature
9. Click **Save Settings** at the bottom to persist to SharePoint

## Verification

After saving:
1. Refresh the page
2. Signatures should reappear on the canvas
3. SMTP settings should be retained
4. Test email should work with new SMTP configuration

## Troubleshooting

### Signatures Not Appearing After Save
- Check browser console for errors
- Verify columns exist in SharePoint list
- Check that column names match exactly (case-sensitive internal names)
- View list item in SharePoint to see if signature data was saved (will be long base64 string)

### SMTP Test Failing
- Verify all SMTP fields are filled (Server, Port, From Address, Username)
- Check SMTP password is set in Azure Key Vault (secret name: `smtp-password`)
- Verify SMTP server and port are correct for your provider
- Check Azure Function logs for detailed error messages

### Columns Already Exist Error
- If you get "column already exists" error, that's fine - just skip that column
- The column may have been added previously or by the provisioner script
