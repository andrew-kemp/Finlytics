# Invoice Issues - Resolution Guide

## Issue 1: Currency Symbols Not Showing in PDF

**Problem**: PDFs show amounts like "800.00" without currency symbol (£).

**Root Cause**: The Currency Symbol field in Company Settings may not be set.

**Solution**:
1. Go to the **Company Details** page in the portal
2. Look for the **Default Currency** field
3. Make sure it's set to **GBP (£)**
4. Save the settings

The PDF generation code already uses `company.CurrencySymbol` throughout, so once the company settings are correct, new PDFs will include the £ symbol.

**Where Currency Symbols Appear**:
- Rate column (per line item)
- Total column (per line item)  
- Subtotal
- Discount Amount
- VAT
- Total Amount
- Payment Details section

---

## Issue 2: Email Sending Fails with "Access Denied"

**Problem**: Cannot send invoice emails - error shows "ErrorAccessDenied - Access is denied"

**Root Cause**: The Function App's Managed Identity doesn't have permission to send email from `invoices@andykemp.com`

**Solution**:

### Step 1: Run the Permission Script

I've created a script: **Grant-EmailPermissions.ps1**

Run it as an **Exchange Administrator or Global Administrator**:

```powershell
# Make sure you have the Exchange Online module
Install-Module -Name ExchangeOnlineManagement -Scope CurrentUser

# Run the grant script
.\Grant-EmailPermissions.ps1
```

### Step 2: Wait for Propagation

After running the script, wait **5-10 minutes** for the permissions to propagate through Azure AD.

### Step 3: Test

Try sending an invoice email from the portal:
1. Click the 📧 button on any invoice
2. Check the browser console for success/error messages
3. Check the recipient's inbox

### Alternative: Manual Permission Grant

If the script doesn't work, you can manually grant permissions:

1. Go to **Exchange Admin Center** (https://admin.exchange.microsoft.com)
2. Navigate to **Recipients** → **Mailboxes**
3. Find and click on **invoices@andykemp.com**
4. Go to **Mailbox Delegation** tab
5. Under **Send As**, click **+ Add permissions**
6. Search for the Function App name: **func-financehub-2669**
7. Select it and click **Save**

---

## Verification

### Check Currency Symbol:
1. Create a new invoice
2. Click "View PDF" 
3. Verify all amounts show £ symbol

### Check Email:
1. Click 📧 on any invoice
2. Should see success message instead of error
3. Check recipient inbox for the email with PDF attachment

---

## Current Status

✅ **Mark as Paid button** - Working (sets status to Paid, records DatePaid)  
✅ **Due Date auto-calculation** - Working (DateIssued + payment terms days)  
✅ **Billing Email population** - Working (fetches from customer record)  
⚠️ **Currency symbols in PDF** - Needs company settings check  
⚠️ **Email sending** - Needs permission grant (see above)  

---

## Notes

- The Currency Symbol setting is in the Company Settings SharePoint list (`CurrencySymbol` field)
- Email permissions can take up to 10 minutes to propagate
- The Managed Identity is: System-assigned identity of the Function App
- You need Exchange Admin or Global Admin rights to grant SendAs permissions
