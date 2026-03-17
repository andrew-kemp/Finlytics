# Finance Hub - Quick Start Guide

## 🚀 Deploy to Azure in 3 Steps

### Step 1: Run Deployment Script (15-20 minutes)

```powershell
# Navigate to Finance Hub directory
cd "c:\Users\AndrewKemp\Kemponline\GitHub - General\FinanceHub"

# Run deployment
.\Deploy-FinanceHub-Azure.ps1 -Environment prod -Location uksouth
```

**What this does:**
- ✅ Creates new Resource Group
- ✅ Provisions Azure SQL Database (Serverless, auto-pause after 1 hour)
- ✅ Creates Function App with Managed Identity
- ✅ Creates Static Web App with Entra SSO
- ✅ Sets up Key Vault with secrets
- ✅ Configures Application Insights
- ✅ Creates Storage Account with containers
- ✅ Registers Entra App for authentication
- ✅ Configures all security and networking

**Outputs saved to:** `deployment-config-prod.json`

### Step 2: Deploy Database Schema (2 minutes)

```powershell
# Create initial migration
cd FunctionApp
dotnet ef migrations add InitialCreate

# Migration will auto-apply on first Function App deployment
```

### Step 3: Deploy Code (5 minutes)

```powershell
# Deploy Function App
cd FunctionApp
func azure functionapp publish <your-function-app-name>

# Deploy Static Web App
cd ../StaticWebApp
npx @azure/static-web-apps-cli deploy --deployment-token "<token-from-step-1>"
```

## ✅ Verification

```powershell
# Test Function App
$funcUrl = "https://<your-function-app-name>.azurewebsites.net"
curl "$funcUrl/api/health"

# Open Static Web App
start "https://<your-static-web-app>.azurestaticapps.net"
```

## 💰 Cost Breakdown

| Resource | Tier | Monthly Cost |
|----------|------|--------------|
| SQL Database | Serverless 0.5-1 vCore | £5-10 |
| Function App | Consumption | FREE |
| Static Web App | Free | FREE |
| Storage Account | Standard LRS | £1-2 |
| Application Insights | Pay-as-you-go | £1-2 |
| Key Vault | Standard | £0.50 |
| **TOTAL** | | **£8-15/month** |

## 🔒 Security Features

- ✅ **Entra SSO** - All users authenticate via Azure AD
- ✅ **Managed Identity** - No passwords in code
- ✅ **Key Vault** - All secrets encrypted
- ✅ **HTTPS Only** - All traffic encrypted
- ✅ **SQL Firewall** - Restricted access
- ✅ **Private Storage** - No public access to blobs

## 🌍 Multi-Tenant Deployment

To deploy for different clients/subscriptions:

```powershell
# Deploy for Client A
.\Deploy-FinanceHub-Azure.ps1 `
    -ResourcePrefix "clienta-financehub" `
    -SubscriptionId "<client-a-subscription>" `
    -Environment prod

# Deploy for Client B
.\Deploy-FinanceHub-Azure.ps1 `
    -ResourcePrefix "clientb-financehub" `
    -SubscriptionId "<client-b-subscription>" `
    -Environment prod
```

Each deployment is completely isolated with its own:
- Resource Group
- Database
- Authentication
- Storage

## 🛠️ Local Development

```powershell
# 1. Restore packages
cd FunctionApp
dotnet restore

# 2. Create local database
dotnet ef database update

# 3. Start Function App
func start

# 4. Start Static Web App (in another terminal)
cd ../StaticWebApp
npm install
npm run dev
```

## 📊 Monitoring

### View Logs
```powershell
# Function App logs
func azure functionapp logstream <app-name>

# Azure Portal
# Application Insights → Logs → Query:
# requests | where timestamp > ago(1h)
```

### SQL Database Metrics
- Azure Portal → SQL Database → Metrics
- Monitor: DTU usage, storage, query performance

## 🔧 Common Tasks

### Update Database Schema
```powershell
cd FunctionApp
dotnet ef migrations add <MigrationName>
func azure functionapp publish <app-name>  # Auto-applies migrations
```

### Update Application Settings
```powershell
az functionapp config appsettings set `
    --name <app-name> `
    --resource-group <rg-name> `
    --settings "SettingName=Value"
```

### Backup Database
```powershell
# Automatic backups enabled by default (7-35 days retention)
# Manual backup:
az sql db export `
    --server <server-name> `
    --name <db-name> `
    --storage-key <storage-key> `
    --storage-key-type StorageAccessKey `
    --storage-uri "https://<storage>.blob.core.windows.net/backups/backup.bacpac"
```

## 🆘 Troubleshooting

### Can't connect to SQL Database
```powershell
# Add your IP to firewall
$myIp = (Invoke-RestMethod "https://api.ipify.org").trim()
az sql server firewall-rule create `
    --name "MyIP" `
    --server <server-name> `
    --resource-group <rg-name> `
    --start-ip-address $myIp `
    --end-ip-address $myIp
```

### Function App can't access SQL
```powershell
# Grant Managed Identity access - run sql-setup-managed-identity.sql
# Connect to your database and execute the script generated during deployment
```

### Static Web App not authenticating
```powershell
# Update app registration redirect URI
az ad app update `
    --id <app-id> `
    --web-redirect-uris "https://<your-swa>.azurestaticapps.net/.auth/login/aad/callback"
```

## 📚 Documentation

- Full README: [README-AZURE-SQL.md](README-AZURE-SQL.md)
- Deployment outputs: `deployment-config-prod.json`
- Deployment logs: `deployment-YYYYMMDD-HHMMSS.log`
- SQL setup script: `sql-setup-managed-identity.sql`

## 🎯 Next Steps

1. ✅ Access your Finance Hub: `https://<your-swa>.azurestaticapps.net`
2. ✅ Configure company settings
3. ✅ Add customers, suppliers, employees
4. ✅ Create invoices and track expenses
5. ✅ Monitor via Application Insights

## 💡 Tips

- **Auto-pause saves money**: SQL Database pauses after 1 hour of inactivity
- **Use local dev**: Free LocalDB for development
- **Monitor costs**: Set up Azure cost alerts
- **Regular backups**: Automated backups included
- **Scale when ready**: Upgrade tiers as usage grows

---

**Need help?** Check deployment logs or review Application Insights for errors.
