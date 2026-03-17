# SharePoint-based Ledger Hub (ARCHIVED)

This folder contains the old SharePoint Online-based provisioning scripts and setup files.

## Why Archived?

The Finance Hub has been migrated to use **Azure SQL Database** instead of SharePoint Online lists.

**Benefits of the new architecture:**
- Direct SQL access (no REST API complexity)
- Better performance and reliability
- Lower operational complexity
- SaaS-ready multi-tenant support
- Cost-optimized (£8-15/month)

## What's Here?

- SharePoint provisioning scripts
- Column setup scripts
- Email and permission configuration scripts
- Old configuration files

## Do I Need These Files?

**No** - if you're deploying the new Azure SQL-based Finance Hub.

**Maybe** - if you need to reference the old SharePoint setup or migrate data.

## New Deployment

Use the new deployment scripts in the parent folder:
- `Deploy-Everything.ps1` - One script to deploy everything
- `Deploy-FinanceHub-Azure.ps1` - Infrastructure only
- See `QUICKSTART.md` for instructions

## Date Archived

2026-02-08 16:03:26

---

These files are kept for reference only. The active Finance Hub uses Azure SQL Database.
