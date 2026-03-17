<#
.SYNOPSIS
    Archive old SharePoint-based Ledger Hub files
    
.DESCRIPTION
    Moves all SharePoint provisioner and related files to Archive-SharePoint folder.
    These files are no longer needed for the new Azure SQL-based Finance Hub.
#>

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$archiveFolder = Join-Path $scriptRoot "Archive-SharePoint"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Archiving Old SharePoint Files" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create archive folder
if (-not (Test-Path $archiveFolder)) {
    New-Item -ItemType Directory -Path $archiveFolder | Out-Null
    Write-Host "✓ Created archive folder: $archiveFolder" -ForegroundColor Green
}

# Files to archive
$filesToArchive = @(
    "02-FinanceHub-ProvisionerV2.ps1"
    "03-EXO-SharedMailboxes.ps1"
    "04-ConfigureAzureResources.ps1"
    "04-Generate-StaticWebApp-Code.ps1"
    "05-Generate-Function-Code.ps1"
    "06-Generate-StaticWebApp-Code.ps1"
    "07-Generate-LogicApp-Workflow.ps1"
    "Add-CompanySettings-Columns.ps1"
    "Add-Missing-Columns.ps1"
    "Add-SharePoint-Columns-REST.ps1"
    "Check-CompanySettings.ps1"
    "Enable-Attachments-Instructions.ps1"
    "Enable-LedgerAttachments.ps1"
    "finance-hub-config.ini"
    "Grant-EmailPermissions.ps1"
    "Quick-Fix-Columns.ps1"
    "Quick-Fix-SharePoint-Columns.ps1"
    "SharePoint-Column-Setup-Instructions.md"
    "Update-AppPermissions.ps1"
    "Update-CurrencySymbol-GraphAPI.ps1"
    "Update-CurrencySymbol.ps1"
    "Update-SMTP-Defaults.ps1"
    "Test-CurrencySymbol.ps1"
)

# Folders to archive
$foldersToArchive = @(
    "Archive"
    "cert-output"
)

$movedCount = 0
$notFoundCount = 0

# Move files
Write-Host "Moving files..." -ForegroundColor Yellow
foreach ($file in $filesToArchive) {
    $sourcePath = Join-Path $scriptRoot $file
    $destPath = Join-Path $archiveFolder $file
    
    if (Test-Path $sourcePath) {
        Move-Item -Path $sourcePath -Destination $destPath -Force
        Write-Host "  ✓ $file" -ForegroundColor Green
        $movedCount++
    } else {
        Write-Host "  - $file (not found)" -ForegroundColor Gray
        $notFoundCount++
    }
}

# Move folders
Write-Host ""
Write-Host "Moving folders..." -ForegroundColor Yellow
foreach ($folder in $foldersToArchive) {
    $sourcePath = Join-Path $scriptRoot $folder
    $destPath = Join-Path $archiveFolder $folder
    
    if (Test-Path $sourcePath) {
        Move-Item -Path $sourcePath -Destination $destPath -Force
        Write-Host "  ✓ $folder\" -ForegroundColor Green
        $movedCount++
    } else {
        Write-Host "  - $folder\ (not found)" -ForegroundColor Gray
        $notFoundCount++
    }
}

# Create README in archive folder
$readmeContent = @"
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
- ``Deploy-Everything.ps1`` - One script to deploy everything
- ``Deploy-FinanceHub-Azure.ps1`` - Infrastructure only
- See ``QUICKSTART.md`` for instructions

## Date Archived

$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

---

These files are kept for reference only. The active Finance Hub uses Azure SQL Database.
"@

$readmePath = Join-Path $archiveFolder "README-ARCHIVE.md"
$readmeContent | Set-Content $readmePath

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Archive Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Green
Write-Host "  Moved: $movedCount items" -ForegroundColor White
Write-Host "  Not found: $notFoundCount items" -ForegroundColor Gray
Write-Host "  Archive location: $archiveFolder" -ForegroundColor White
Write-Host ""
Write-Host "Your workspace is now clean and ready for the new SQL-based Finance Hub!" -ForegroundColor Green
Write-Host ""
