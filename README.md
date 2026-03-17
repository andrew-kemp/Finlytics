# Finance Hub

**Modern cloud-native financial management system powered by Azure SQL Database**

> 🎯 **Quick Start**: Run `.\Deploy-Everything.ps1` to deploy everything in ~30 minutes!

## What's New

This is the **Azure SQL Edition** - a complete rewrite from the SharePoint-based Ledger Hub:
- ✅ Azure SQL Database (Serverless) - Fast, reliable, auto-pausing
- ✅ Entity Framework Core - Clean LINQ queries
- ✅ Repository Pattern - No more REST API wrangling
- ✅ Managed Identity - Zero passwords in code
- ✅ Entra SSO - Built-in authentication
- ✅ Cost-optimized - **£8-15/month** for <10k transactions/year
- ✅ SaaS-ready - Deploy per tenant in minutes

## Documentation

- **[QUICKSTART.md](QUICKSTART.md)** - Deploy in 30 minutes
- **[README-AZURE-SQL.md](README-AZURE-SQL.md)** - Complete documentation
- **[DEPLOYMENT-CHECKLIST.md](DEPLOYMENT-CHECKLIST.md)** - Step-by-step guide
- **[MIGRATION-SUMMARY.md](MIGRATION-SUMMARY.md)** - Architecture overview

## Deployment

### One Command to Deploy Everything

```powershell
.\Deploy-Everything.ps1
```

This single script handles:
1. Prerequisites check
2. Azure infrastructure deployment
3. Database schema creation
4. Function App deployment
5. Static Web App deployment
6. Authentication configuration
7. Verification tests

**Features:**
- ✅ Fully interactive with saved preferences
- ✅ Resume from checkpoint if it fails
- ✅ Complete logging
- ✅ Multi-tenant ready

### What Gets Deployed

- Azure SQL Database (Serverless, auto-pause)
- Azure Function App (Consumption plan)
- Static Web App (Free tier with CDN)
- Key Vault (secrets management)
- Application Insights (monitoring)
- Storage Account (documents)
- Entra App Registration (SSO)

## Local Development

```powershell
# Setup database
cd FunctionApp
dotnet ef database update

# Run Function App
func start

# Run Static Web App (in another terminal)
cd StaticWebApp
npm install && npm run dev
```

## Architecture

```
Frontend (Static Web App)
    ↓ HTTPS + Entra SSO
Backend (Azure Functions)
    ↓ Managed Identity
Database (Azure SQL Serverless)
    ↓ Auto-pause after 1 hour
Secrets (Key Vault)
    ↓ Managed Identity
Storage (Blob Storage)
```

## Cost Breakdown

| Resource | Monthly Cost |
|----------|--------------|
| SQL Database (Serverless) | £5-10 |
| Function App (Consumption) | FREE |
| Static Web App | FREE |
| Storage + Insights + KV | £3-5 |
| **Total** | **£8-15** |

## Old SharePoint Version

The original SharePoint-based provisioner has been archived to `Archive-SharePoint/`.
It's kept for reference but is **no longer maintained**.

**Why we migrated:**
- ❌ Complex REST API integration
- ❌ Manual JSON parsing
- ❌ "Circular save issues"
- ❌ Limited query capabilities
- ❌ SharePoint licensing dependency

The new SQL-based version solves all these issues!

## Support

- Check [QUICKSTART.md](QUICKSTART.md) for common issues
- Review deployment logs in `master-deployment-*.log`
- See Application Insights for runtime errors
