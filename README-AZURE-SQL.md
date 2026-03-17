# Finance Hub - Azure SQL Edition

A complete financial management system for small businesses, now powered by Azure SQL Database for improved performance, reliability, and SaaS readiness.

## Architecture

### Azure Resources
- **Azure SQL Database (Serverless)** - Auto-pausing database with minimal cost
- **Azure Functions (Consumption Plan)** - Serverless API backend
- **Static Web App (Free Tier)** - Frontend with built-in Entra SSO
- **Key Vault** - Secure secrets management
- **Application Insights** - Monitoring and diagnostics
- **Storage Account** - Document storage (invoices, receipts, certificates)

### Technology Stack
- **Backend**: .NET 8, Azure Functions, Entity Framework Core 8
- **Database**: Azure SQL Database with EF Core migrations
- **Frontend**: Static Web App with Entra authentication
- **Security**: Managed Identity, Key Vault, Entra SSO

## Cost Estimate

For <10,000 transactions/year:
- **SQL Database**: £5-10/month (serverless with auto-pause)
- **Function App**: FREE (under 1M executions/month)
- **Static Web App**: FREE
- **Storage Account**: £1-2/month
- **Application Insights**: £1-2/month
- **Key Vault**: £0.50/month

**Total: ~£8-15/month**

## Deployment

### Prerequisites
1. Azure CLI installed
2. Azure subscription with appropriate permissions
3. .NET 8 SDK
4. Node.js and npm (for Static Web App)

### Quick Start

```powershell
# Clone the repository
cd FinanceHub

# Run the deployment script
.\Deploy-FinanceHub-Azure.ps1 -Environment prod -Location uksouth

# Follow the prompts and note the deployment outputs
```

### Manual Deployment Steps

If you prefer manual control:

```powershell
# 1. Deploy Azure infrastructure
.\Deploy-FinanceHub-Azure.ps1 -Environment dev

# 2. Deploy Function App
cd FunctionApp
func azure functionapp publish financehub-func-dev-XXXX

# 3. Deploy Static Web App
cd ../StaticWebApp
npx @azure/static-web-apps-cli deploy --deployment-token "<token-from-deployment>"
```

### Multi-Tenant Deployment

To deploy for a different tenant/subscription:

```powershell
# Login to target tenant
az login --tenant <tenant-id>

# Set subscription
az account set --subscription <subscription-id>

# Deploy with unique prefix
.\Deploy-FinanceHub-Azure.ps1 `
    -Environment prod `
    -ResourcePrefix clientname-financehub `
    -SubscriptionId <subscription-id>
```

## Database Migrations

The database schema is managed using EF Core migrations.

### Create a Migration

```powershell
cd FunctionApp
dotnet ef migrations add MigrationName
```

### Apply Migrations

Migrations are automatically applied on Function App startup. To manually apply:

```powershell
dotnet ef database update
```

### View Migration Status

```powershell
dotnet ef migrations list
```

## Local Development

### Setup Local Database

Option 1: SQL Server LocalDB (recommended for Windows)
```powershell
# LocalDB is included with Visual Studio
# Connection string is already configured in local.settings.json
```

Option 2: SQL Server Express
```powershell
# Update local.settings.json with your SQL Server connection string
```

Option 3: Azure SQL Database
```powershell
# Use your dev Azure SQL instance
# Update ConnectionStrings:FinanceHubDb in local.settings.json
```

### Run Locally

```powershell
# Start Function App
cd FunctionApp
func start

# In another terminal, start Static Web App
cd StaticWebApp
npm install
npm run dev
```

### Test Endpoints

```powershell
# Get customers
curl http://localhost:7071/api/customers

# Create customer
curl -X POST http://localhost:7071/api/customers -H "Content-Type: application/json" -d "{\"name\":\"Test Customer\",\"email\":\"test@example.com\"}"
```

## Configuration

### Environment Variables

#### Function App
- `KeyVaultUrl` - Key Vault URL (Azure only)
- `ConnectionStrings:FinanceHubDb` - Database connection string (local dev)
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - App Insights connection string

#### Static Web App
- Configured via `staticwebapp.config.json`
- Entra authentication configured during deployment

### Key Vault Secrets
- `SqlConnectionString` - SQL Database connection string
- `StorageConnectionString` - Storage account connection string
- `SqlAdminPassword` - SQL admin password

## Security

### Authentication
- **Frontend**: Entra SSO (Azure AD) with automatic login
- **Backend**: Managed Identity for Azure resource access
- **Database**: Managed Identity authentication (no passwords in code)

### Authorization
- Row-level security (future enhancement for multi-tenant)
- Role-based access control via Entra groups
- API-level authorization checks

### Secrets Management
- All secrets stored in Key Vault
- Managed Identity used for secret retrieval
- No connection strings or passwords in source code

## Monitoring

### Application Insights
- View logs: Azure Portal → Application Insights → Logs
- Metrics: Requests, failures, response times
- Live metrics for real-time monitoring

### SQL Database Metrics
- DTU/CPU usage
- Storage consumption
- Query performance

### Function App Logs
```powershell
# Stream logs
func azure functionapp logstream financehub-func-prod-XXXX
```

## Troubleshooting

### Database Connection Issues

```powershell
# Check firewall rules
az sql server firewall-rule list --server <server-name> --resource-group <rg-name>

# Add your IP
az sql server firewall-rule create --name MyIP --server <server-name> --resource-group <rg-name> --start-ip-address <your-ip> --end-ip-address <your-ip>
```

### Managed Identity Issues

```powershell
# Verify Function App identity
az functionapp identity show --name <app-name> --resource-group <rg-name>

# Grant SQL access - run the generated sql-setup-managed-identity.sql script
```

### Migration Issues

```powershell
# Drop database and recreate (DEV ONLY!)
dotnet ef database drop
dotnet ef database update

# View pending migrations
dotnet ef migrations list
```

## Upgrading from SharePoint Version

The legacy SharePointService.cs is retained for reference but not used. To migrate data:

1. Export data from SharePoint lists
2. Transform to SQL format
3. Import using provided migration scripts (coming soon)

## Contributing

### Adding New Entities

1. Create model in `Models/`
2. Add DbSet to `FinanceHubDbContext`
3. Configure entity in `OnModelCreating`
4. Create migration: `dotnet ef migrations add AddEntityName`
5. Create repository interface and implementation
6. Register repository in `Program.cs`
7. Create Function endpoints

### Code Style
- Follow C# naming conventions
- Use async/await consistently
- Add XML comments for public APIs
- Keep functions focused and testable

## Support

For issues or questions:
1. Check the deployment log files
2. Review Application Insights logs
3. Verify Key Vault access
4. Check SQL Database firewall rules

## License

Private/Internal Use Only
