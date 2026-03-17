# Company Document Templates

This folder contains HTML templates for generating company documents like share certificates, dividend vouchers, and board minutes. These templates pull data from your SharePoint Company Settings list and other data sources.

## Available Templates

1. **Board Minutes — Declaring a Dividend.html** - Board meeting minutes for dividend declarations
2. **Share Certificate — A Ordinary Shares.html** - Certificate for A Ordinary shares
3. **Share Certificate — B Ordinary Shares.html** - Certificate for B Ordinary shares
4. **Dividend Voucher — per Shareholder.html** - Dividend payment voucher for individual shareholders

## Features

All templates include:
- ✅ **Logo placeholder** - "LOGO HERE" box that can be replaced with your company logo
- ✅ **Signature capture** - Digital signature functionality using pen/stylus/touch/mouse
- ✅ **Director signature** - Canvas for director to sign documents
- ✅ **Authorized officer signature** - Optional second signature (with "Use Director" button to copy)
- ✅ **Print-friendly** - Signature buttons hidden when printing
- ✅ **Dynamic placeholders** - Company info pulled from SharePoint

## Placeholders

### Company Information (Auto-populated from SharePoint Company Settings)
- `{{COMPANY_NAME}}` - Company name
- `{{COMPANY_NUMBER}}` - Company registration number
- `{{REGISTERED_OFFICE_ADDRESS}}` - Registered office address

### Shareholder/Certificate Information
- `{{HOLDER_FULL_NAME}}` - Shareholder full name
- `{{SHARE_CLASS}}` - Share class (A Ordinary, B Ordinary, etc.)
- `{{NUMBER_OF_SHARES}}` - Number of shares
- `{{ISSUE_DATE_DD_MON_YYYY}}` - Issue date formatted as "01 Jan 2026"
- `{{FROM_TO_NUMBERS}}` - Share number range (e.g., "1-100")
- `{{YEAR}}` - Current year
- `{{SEQUENCE}}` - Sequential certificate/voucher number

### Dividend Information
- `{{MEETING_DATE}}` - Board meeting date
- `{{MEETING_TIME_AND_PLACE}}` - Meeting time and location
- `{{DIRECTOR_NAME}}` - Director's name
- `{{AUTHORIZED_OFFICER_NAME}}` - Authorized officer name (if different from director)
- `{{ACCOUNTS_TO_DATE}}` - Accounts period end date
- `{{AMOUNT_PER_SHARE}}` - Dividend per share
- `{{PAYMENT_DATE}}` - Payment date
- `{{RECORD_DATE}}` - Record date for eligibility
- `{{SIGN_DATE}}` - Signature date
- `{{GROSS_AMOUNT}}` - Gross dividend amount
- `{{NET_AMOUNT}}` - Net dividend amount

## How to Use

### Using the Template Helper JavaScript

The `template-helper.js` file provides utilities to populate templates with data:

```javascript
// Initialize the helper
const helper = new TemplateHelper();
await helper.initialize(); // Fetches company settings from SharePoint

// Generate a share certificate
const certificateHTML = await helper.generateShareCertificate(
    'Share Certificate — A Ordinary Shares.html',
    {
        shareholderName: 'John Smith',
        sharesOwned: 100,
        issueDate: new Date('2026-01-15'),
        certificateNumber: '001',
        directorName: 'Andrew Kemp'
    }
);

// Generate board minutes
const minutesHTML = await helper.generateBoardMinutes({
    meetingDate: new Date('2026-02-01'),
    meetingTimePlace: '14:00, Virtual Meeting',
    directorName: 'Andrew Kemp',
    accountsDate: new Date('2025-12-31'),
    shareClass: 'A Ordinary',
    amountPerShare: '1.50',
    paymentDate: new Date('2026-02-15'),
    recordDate: new Date('2026-02-01')
});

// Generate dividend voucher
const voucherHTML = await helper.generateDividendVoucher({
    shareholderName: 'John Smith',
    shareClass: 'A Ordinary',
    numberOfShares: 100,
    amountPerShare: '1.50',
    grossAmount: '150.00',
    paymentDate: new Date('2026-02-15'),
    recordDate: new Date('2026-02-01'),
    directorName: 'Andrew Kemp',
    voucherNumber: '001'
});
```

### Using Signatures

The templates include built-in signature capture:

1. **Draw a signature** - Use mouse, finger, or stylus on the canvas
2. **Clear** - Remove the signature and start over
3. **Save** - Store the signature in browser localStorage for reuse
4. **Use Director** - Copy the director's signature to the officer signature field

Signatures are stored locally in the browser and will automatically load when you reopen the template.

### Integrating with Your Static Web App

To integrate these templates into your FinanceHub Static Web App:

1. **Copy the helper** to your StaticWebApp/src folder:
   ```bash
   cp template-helper.js StaticWebApp/src/utils/
   ```

2. **Use in your React components**:
   ```jsx
   import { TemplateHelper } from '../utils/template-helper';
   
   const generateCertificate = async (shareholder) => {
       const helper = new TemplateHelper();
       await helper.initialize();
       const html = await helper.generateShareCertificate(
           'Share Certificate — A Ordinary Shares.html',
           {
               shareholderName: shareholder.name,
               sharesOwned: shareholder.sharesOwned,
               // ... other data
           }
       );
       
       // Open in new window or convert to PDF
       const newWindow = window.open();
       newWindow.document.write(html);
   };
   ```

3. **Add to Shareholders component** to generate certificates
4. **Add dividend voucher generation** when declaring dividends

## Customization

### Adding Your Company Logo

Replace the logo placeholder with your actual logo:

```html
<!-- Before -->
<div class="logo-placeholder">LOGO HERE</div>

<!-- After -->
<img src="{{LOGO_URL}}" alt="Company Logo" style="width: 120px; height: 60px;">
```

The `{{LOGO_URL}}` placeholder can be populated from your Company Settings `LogoUrl` field.

### Styling

All templates use minimal, clean styling that prints well. You can customize:
- Colors by editing CSS variables
- Fonts by changing the `font-family` in styles
- Layout by modifying grid/flexbox settings

## SharePoint Integration

The templates are designed to work with your SharePoint lists:

- **Company Settings** - Provides company name, number, address
- **Shareholders** - Provides shareholder information
- **Share Classes** - Provides share class details
- **Directors** - Provides director names and titles

Make sure your Azure Function API endpoints are accessible:
- `GET /api/companysettings` - Returns company settings
- `GET /api/shareholders` - Returns shareholders list
- `GET /api/shareholders/{id}` - Returns specific shareholder

## Deployment

Templates can be:
1. **Stored in SharePoint** - Upload to Company Documents library with type "Template"
2. **Embedded in Static Web App** - Copy to `StaticWebApp/public/templates/`
3. **Loaded dynamically** - Fetch from any web location

## Support

For questions or issues:
- Check the `template-helper.js` comments for API details
- Review the placeholder list above
- Test signatures on different devices (tablet with stylus works best)
