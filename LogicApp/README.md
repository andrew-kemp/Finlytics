# Logic App - Unpaid Invoice Reminders

This Logic App sends automated email reminders for overdue invoices.

## Configuration

- **Schedule:** Every 2 days at 9:00 AM (GMT Standard Time)
- **Days Overdue Threshold:** 7 days
- **From Email:** invoices@andykemp.com
- **CC:** andrew@kemponline.co.uk

## Deployment

Run: `.\deploy-logicapp.ps1` or `./deploy-logicapp.sh`

Then configure Office 365 connection in Azure Portal.

## Workflow

1. Recurrence trigger (every 2 days at 9am)
2. Get unpaid invoices from Function App
3. Send reminder email for each invoice

## Support

Contact: andrew@kemponline.co.uk
