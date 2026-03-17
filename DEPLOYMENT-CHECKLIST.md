# Finance Hub Deployment Checklist

Use this checklist to deploy your Finance Hub to Azure step-by-step.

## Prerequisites ✓

- [ ] Azure CLI installed (`az --version`)
- [ ] .NET 8 SDK installed (`dotnet --version`)
- [ ] Azure Functions Core Tools installed (`func --version`)
- [ ] Node.js and npm installed (`node --version`)
- [ ] Azure subscription with appropriate permissions
- [ ] Logged into Azure (`az login`)
- [ ] Partner credits available (optional but recommended)

## Phase 1: Prepare Environment (5 min)

- [ ] Open PowerShell as Administrator
- [ ] Navigate to Finance Hub directory
- [ ] Verify all prerequisites above
- [ ] Review deployment parameters (subscription, location, environment)
- [ ] Take note of current date/time for log reference

## Phase 2: Deploy Azure Infrastructure (15-20 min)

- [ ] Run: `.\Deploy-FinanceHub-Azure.ps1 -Environment prod -Location uksouth`
- [ ] Wait for deployment to complete
- [ ] Review deployment logs in `deployment-<timestamp>.log`
- [ ] Save `deployment-config-prod.json` for reference
- [ ] Save `sql-setup-managed-identity.sql` script
- [ ] Note Function App name: ________________________________
- [ ] Note Static Web App name: ________________________________
- [ ] Note SQL Server name: ________________________________
- [ ] Note Key Vault name: ________________________________

## Phase 3: Configure Database Access (5 min)

- [ ] Open Azure Portal
- [ ] Navigate to SQL Server created above
- [ ] Go to SQL Database → Query Editor
- [ ] Login with SQL Admin credentials (from Key Vault or deployment log)
- [ ] Run the `sql-setup-managed-identity.sql` script
- [ ] Verify "Command completed successfully" message

## Phase 4: Initialize Database Schema (2 min)

- [ ] Open PowerShell in Finance Hub directory
- [ ] Run: `cd FunctionApp`
- [ ] Run: `dotnet restore`
- [ ] Run: `dotnet ef migrations add InitialCreate`
- [ ] Verify migration files created in `Migrations` folder
- [ ] Review migration code (optional)

## Phase 5: Deploy Function App (5 min)

- [ ] Still in FunctionApp directory
- [ ] Run: `dotnet build`
- [ ] Verify build succeeds with no errors
- [ ] Run: `func azure functionapp publish <function-app-name>`
- [ ] Wait for deployment to complete
- [ ] Verify "Deployment successful" message
- [ ] Check Function App URL: https://___________.azurewebsites.net

## Phase 6: Deploy Static Web App (5 min)

- [ ] Open new PowerShell window
- [ ] Navigate to StaticWebApp directory
- [ ] Run: `npm install`
- [ ] Get deployment token from `deployment-config-prod.json`
- [ ] Run: `npx @azure/static-web-apps-cli deploy --deployment-token "<token>"`
- [ ] Wait for deployment to complete
- [ ] Note Static Web App URL: https://___________.azurestaticapps.net

## Phase 7: Configure Authentication (5 min)

- [ ] Open Azure Portal
- [ ] Navigate to Entra ID (Azure Active Directory)
- [ ] Go to App Registrations
- [ ] Find "FinanceHub-prod" app registration
- [ ] Go to Authentication
- [ ] Add Redirect URI: `https://<your-swa>.azurestaticapps.net/.auth/login/aad/callback`
- [ ] Save changes
- [ ] Add your user account if needed

## Phase 8: Verify Deployment (10 min)

### Test Function App
- [ ] Open browser to Function App URL
- [ ] Should see "Your Functions 4.0 app is up and running"
- [ ] Test health endpoint (if you create one)

### Test Static Web App
- [ ] Open browser to Static Web App URL
- [ ] Should redirect to Microsoft login
- [ ] Login with your Entra credentials
- [ ] Should see Finance Hub homepage
- [ ] Check browser console for errors (should be none)

### Test Database
- [ ] In Azure Portal, go to SQL Database
- [ ] Check "Query performance insight"
- [ ] Should show initial table creation queries
- [ ] Verify tables exist (run: `SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES`)

### Test Application Insights
- [ ] Open Application Insights in Azure Portal
- [ ] Go to "Live Metrics"
- [ ] Refresh your Finance Hub app
- [ ] Should see requests appearing in real-time
- [ ] Check for any errors or warnings

## Phase 9: Initial Configuration (10 min)

- [ ] Login to Finance Hub
- [ ] Go to Company Settings
- [ ] Configure company details:
  - [ ] Company name
  - [ ] Company number
  - [ ] VAT number
  - [ ] Address
  - [ ] Currency (GBP/£)
  - [ ] Financial year start/end
  - [ ] Bank details
- [ ] Save settings
- [ ] Verify settings saved successfully

## Phase 10: Test Core Functions (15 min)

### Test Customers
- [ ] Navigate to Customers page
- [ ] Click "Add Customer"
- [ ] Enter test customer details
- [ ] Save
- [ ] Verify customer appears in list
- [ ] Edit customer
- [ ] Delete test customer

### Test Ledger
- [ ] Navigate to Company Ledger
- [ ] Add a test entry
- [ ] Select entry type (e.g., "Salary")
- [ ] Enter amount and date
- [ ] Save
- [ ] Verify entry appears
- [ ] Check aggregates/overview
- [ ] Delete test entry

### Test Suppliers (if implemented)
- [ ] Add test supplier
- [ ] Verify saved
- [ ] Delete test supplier

### Test Invoices (if implemented)
- [ ] Create test invoice
- [ ] Add line items
- [ ] Generate PDF
- [ ] Verify PDF generated
- [ ] Delete test invoice

## Phase 11: Monitoring Setup (5 min)

### Configure Alerts
- [ ] In Azure Portal, go to Application Insights
- [ ] Go to Alerts
- [ ] Create alert rule for:
  - [ ] Failed requests > 5 in 5 minutes
  - [ ] Response time > 3 seconds
  - [ ] Availability < 95%

### Configure Cost Alerts
- [ ] Go to Cost Management
- [ ] Create budget
- [ ] Set threshold: £20/month
- [ ] Configure email notification

### Verify Auto-Pause
- [ ] Wait 1 hour without activity
- [ ] Check SQL Database status
- [ ] Should show "Paused"
- [ ] Access Finance Hub
- [ ] Database should auto-resume

## Phase 12: Backup & Disaster Recovery (5 min)

- [ ] Verify automated backups enabled (SQL Database settings)
- [ ] Note backup retention period: ______ days
- [ ] Export deployment configuration
- [ ] Save deployment scripts to version control
- [ ] Document any manual configuration steps

## Phase 13: Performance Baseline (5 min)

Record initial metrics:
- [ ] SQL Database: ______ MB storage used
- [ ] Function App: ______ executions today
- [ ] Static Web App: ______ visits today
- [ ] Application Insights: ______ requests today
- [ ] Estimated cost for day 1: £______

## Phase 14: Documentation (5 min)

- [ ] Update internal wiki/docs with:
  - [ ] Function App URL
  - [ ] Static Web App URL
  - [ ] SQL Server connection details (admin)
  - [ ] Key Vault name
  - [ ] Resource Group name
- [ ] Share access with team members
- [ ] Add to password manager (SQL admin password from Key Vault)
- [ ] Schedule review meeting

## Phase 15: Cleanup (Optional)

If this was a test deployment and you want to remove it:

- [ ] Run: `az group delete --name <resource-group-name> --yes --no-wait`
- [ ] Remove app registration from Entra
- [ ] Remove any local test data

## Success Criteria

✅ All checkboxes above are complete
✅ No errors in Application Insights
✅ Users can login via Entra
✅ Can create/read/update/delete records
✅ SQL database auto-pauses
✅ Cost is within budget (£8-15/month)
✅ Team has access to documentation

## Troubleshooting

If you encounter issues, refer to:
- `QUICKSTART.md` - Common issues and solutions
- `README-AZURE-SQL.md` - Detailed documentation
- `deployment-<timestamp>.log` - Deployment logs
- Application Insights → Failures

## Next Deployment

For deploying to a different tenant:

- [ ] Repeat from Phase 2 with different:
  - [ ] Subscription ID
  - [ ] Resource Prefix
  - [ ] Environment name

---

## Completion

**Deployment completed on**: ________________________

**Deployed by**: ________________________

**Total time taken**: __________ minutes

**Any issues encountered**: 

_____________________________________________________________

_____________________________________________________________

**Sign off**: ___________________  Date: ________________

---

Save this completed checklist for your records!
