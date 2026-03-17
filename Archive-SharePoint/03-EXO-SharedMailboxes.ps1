<#
    01b-EXO-SharedMailboxes.ps1
    Provisions shared mailboxes in Exchange Online for FinanceHub
    
    Creates:
      - quotes@<domain> shared mailbox
      - invoices@<domain> shared mailbox
      - Grants FullAccess and SendAs permissions to specified users
    
    Requires:
      - ExchangeOnlineManagement PowerShell module
      - Exchange Administrator or Global Administrator role
      - finance-hub-config.ini (created by 01a-Pre-Requisites.ps1)
#>

param()

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"

# ---------------------------
# INI helpers
# ---------------------------
function Get-IniContent {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @{} }
    $ini = @{}
    $section = ""
    switch -regex -file $Path {
        "^\s*\[(.+)\]\s*$" {
            $section = $matches[1]
            $ini[$section] = @{}
        }
        "^\s*([^=]+?)\s*=\s*(.*)$" {
            if (-not $section) { $section = "Default"; $ini[$section] = @{} }
            $name = $matches[1].Trim()
            $value = $matches[2]
            $ini[$section][$name] = $value
        }
    }
    return $ini
}

function Set-IniValue {
    param([string]$Path,[string]$Section,[string]$Key,[string]$Value)
    if (-not (Test-Path $Path)) { "" | Set-Content $Path }
    $ini = Get-IniContent $Path
    if (-not $ini.ContainsKey($Section)) { $ini[$Section] = @{} }
    $ini[$Section][$Key] = $Value

    $sb = New-Object System.Text.StringBuilder
    foreach ($sec in $ini.Keys) {
        [void]$sb.AppendLine("[$sec]")
        foreach ($k in $ini[$sec].Keys) {
            [void]$sb.AppendLine("$k=$($ini[$sec][$k])")
        }
        [void]$sb.AppendLine("")
    }
    $sb.ToString() | Set-Content -Path $Path -Encoding UTF8
}

# ---------------------------
# Banner
# ---------------------------
Clear-Host
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " FinanceHub - Exchange Online Setup" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# ---------------------------
# Check for config file
# ---------------------------
if (-not (Test-Path $configFile)) {
    Write-Host "ERROR: Configuration file not found!" -ForegroundColor Red
    Write-Host "Please run 01a-Pre-Requisites.ps1 first." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

$config = Get-IniContent $configFile
Write-Host "Loading configuration from: $configFile" -ForegroundColor Gray
Write-Host ""

# ---------------------------
# Validate required config
# ---------------------------
if (-not $config.ContainsKey("Email")) {
    Write-Host "ERROR: Email configuration not found in config file!" -ForegroundColor Red
    Write-Host "Please run 01a-Pre-Requisites.ps1 first." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

$quotesEmail = $config.Email.QuotesFromEmail
$invoicesEmail = $config.Email.InvoicesFromEmail
$sharedMailboxUsers = $config.Email.SharedMailboxUsers

if ([string]::IsNullOrWhiteSpace($quotesEmail) -or [string]::IsNullOrWhiteSpace($invoicesEmail)) {
    Write-Host "ERROR: Email addresses not configured!" -ForegroundColor Red
    Write-Host "Please run 01a-Pre-Requisites.ps1 to configure email settings." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "Configuration loaded:" -ForegroundColor Green
Write-Host "  Quotes Email:    $quotesEmail" -ForegroundColor White
Write-Host "  Invoices Email:  $invoicesEmail" -ForegroundColor White
Write-Host "  Access Users:    $sharedMailboxUsers" -ForegroundColor White
Write-Host ""

# ---------------------------
# Install/Import ExchangeOnlineManagement module
# ---------------------------
Write-Host "Checking for ExchangeOnlineManagement module..." -ForegroundColor Yellow
if (-not (Get-Module -ListAvailable -Name ExchangeOnlineManagement)) {
    Write-Host "Installing ExchangeOnlineManagement module for current user..." -ForegroundColor Cyan
    Install-Module ExchangeOnlineManagement -Scope CurrentUser -Force -AllowClobber
}
Import-Module ExchangeOnlineManagement
Write-Host "  ✓ Module loaded" -ForegroundColor Green
Write-Host ""

# ---------------------------
# Connect to Exchange Online
# ---------------------------
Write-Host "Connecting to Exchange Online..." -ForegroundColor Yellow
Write-Host "A browser window will open for authentication." -ForegroundColor Gray
Write-Host ""

try {
    Connect-ExchangeOnline -ShowBanner:$false -ErrorAction Stop
    Write-Host "  ✓ Connected to Exchange Online" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "ERROR: Failed to connect to Exchange Online" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure you have:" -ForegroundColor Yellow
    Write-Host "  - Exchange Administrator or Global Administrator role" -ForegroundColor White
    Write-Host "  - Modern authentication enabled" -ForegroundColor White
    Write-Host ""
    exit 1
}

# ---------------------------
# Function to create or verify shared mailbox
# ---------------------------
function Ensure-SharedMailbox {
    param(
        [string]$EmailAddress,
        [string]$DisplayName
    )
    
    Write-Host "Checking shared mailbox: $EmailAddress" -ForegroundColor Cyan
    
    try {
        $mailbox = Get-Mailbox -Identity $EmailAddress -ErrorAction SilentlyContinue
        
        if ($mailbox) {
            Write-Host "  ✓ Mailbox already exists" -ForegroundColor Green
            
            if ($mailbox.RecipientTypeDetails -ne "SharedMailbox") {
                Write-Host "  ⚠ Warning: Mailbox exists but is not a Shared Mailbox (it's $($mailbox.RecipientTypeDetails))" -ForegroundColor Yellow
                Write-Host "  Continuing anyway..." -ForegroundColor Gray
            }
            
            return $mailbox
        }
    } catch {
        # Mailbox doesn't exist, proceed to create
    }
    
    Write-Host "  Creating new shared mailbox..." -ForegroundColor Yellow
    
    try {
        $mailbox = New-Mailbox -Shared -Name $DisplayName -DisplayName $DisplayName -PrimarySmtpAddress $EmailAddress -ErrorAction Stop
        Write-Host "  ✓ Shared mailbox created successfully" -ForegroundColor Green
        
        # Wait for mailbox to propagate
        Write-Host "  Waiting 10 seconds for mailbox to propagate..." -ForegroundColor Gray
        Start-Sleep -Seconds 10
        
        return $mailbox
    } catch {
        Write-Host "  ✗ Failed to create mailbox" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

# ---------------------------
# Function to grant permissions
# ---------------------------
function Grant-SharedMailboxPermissions {
    param(
        [string]$MailboxIdentity,
        [string[]]$Users
    )
    
    Write-Host "Granting permissions for: $MailboxIdentity" -ForegroundColor Cyan
    
    foreach ($user in $Users) {
        $userTrimmed = $user.Trim()
        if ([string]::IsNullOrWhiteSpace($userTrimmed)) { continue }
        
        Write-Host "  Processing user: $userTrimmed" -ForegroundColor White
        
        # Grant FullAccess
        try {
            Add-MailboxPermission -Identity $MailboxIdentity -User $userTrimmed -AccessRights FullAccess -InheritanceType All -ErrorAction Stop | Out-Null
            Write-Host "    ✓ FullAccess granted" -ForegroundColor Green
        } catch {
            if ($_.Exception.Message -match "already exists") {
                Write-Host "    ✓ FullAccess already granted" -ForegroundColor Gray
            } else {
                Write-Host "    ✗ Failed to grant FullAccess: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        
        # Grant SendAs
        try {
            Add-RecipientPermission -Identity $MailboxIdentity -Trustee $userTrimmed -AccessRights SendAs -Confirm:$false -ErrorAction Stop | Out-Null
            Write-Host "    ✓ SendAs granted" -ForegroundColor Green
        } catch {
            if ($_.Exception.Message -match "already exists") {
                Write-Host "    ✓ SendAs already granted" -ForegroundColor Gray
            } else {
                Write-Host "    ✗ Failed to grant SendAs: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

# ---------------------------
# Create Quotes shared mailbox
# ---------------------------
Write-Host ""
Write-Host "=== Creating Quotes Shared Mailbox ===" -ForegroundColor Green
Write-Host ""

try {
    $quotesMailbox = Ensure-SharedMailbox -EmailAddress $quotesEmail -DisplayName "FinanceHub Quotes"
    Set-IniValue -Path $configFile -Section "Email" -Key "QuotesMailboxCreated" -Value "Yes"
} catch {
    Write-Host ""
    Write-Host "Failed to create quotes mailbox. Continuing..." -ForegroundColor Yellow
    Write-Host ""
}

# ---------------------------
# Create Invoices shared mailbox
# ---------------------------
Write-Host ""
Write-Host "=== Creating Invoices Shared Mailbox ===" -ForegroundColor Green
Write-Host ""

try {
    $invoicesMailbox = Ensure-SharedMailbox -EmailAddress $invoicesEmail -DisplayName "FinanceHub Invoices"
    Set-IniValue -Path $configFile -Section "Email" -Key "InvoicesMailboxCreated" -Value "Yes"
} catch {
    Write-Host ""
    Write-Host "Failed to create invoices mailbox. Continuing..." -ForegroundColor Yellow
    Write-Host ""
}

# ---------------------------
# Grant permissions
# ---------------------------
Write-Host ""
Write-Host "=== Granting Permissions ===" -ForegroundColor Green
Write-Host ""

if ([string]::IsNullOrWhiteSpace($sharedMailboxUsers)) {
    Write-Host "No users specified for mailbox access." -ForegroundColor Yellow
    $addUsers = Read-Host "Would you like to add users now? (Y/N)"
    
    if ($addUsers -eq "Y" -or $addUsers -eq "y") {
        $sharedMailboxUsers = Read-Host "Enter user email addresses (comma-separated)"
        Set-IniValue -Path $configFile -Section "Email" -Key "SharedMailboxUsers" -Value $sharedMailboxUsers
    }
}

if (-not [string]::IsNullOrWhiteSpace($sharedMailboxUsers)) {
    $userList = $sharedMailboxUsers -split ','
    
    Write-Host ""
    Write-Host "--- Quotes Mailbox Permissions ---" -ForegroundColor Cyan
    Grant-SharedMailboxPermissions -MailboxIdentity $quotesEmail -Users $userList
    
    Write-Host ""
    Write-Host "--- Invoices Mailbox Permissions ---" -ForegroundColor Cyan
    Grant-SharedMailboxPermissions -MailboxIdentity $invoicesEmail -Users $userList
} else {
    Write-Host "Skipping permission grants (no users specified)." -ForegroundColor Gray
}

# ---------------------------
# Summary
# ---------------------------
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Exchange Online Setup Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Shared mailboxes created/verified:" -ForegroundColor Yellow
Write-Host "  ✓ $quotesEmail" -ForegroundColor White
Write-Host "  ✓ $invoicesEmail" -ForegroundColor White
Write-Host ""

if (-not [string]::IsNullOrWhiteSpace($sharedMailboxUsers)) {
    Write-Host "Permissions granted to:" -ForegroundColor Yellow
    foreach ($user in ($sharedMailboxUsers -split ',')) {
        Write-Host "  • $($user.Trim())" -ForegroundColor White
    }
    Write-Host ""
}

Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Users can access mailboxes in Outlook (may take 15-30 minutes to appear)" -ForegroundColor White
Write-Host "  2. Run 01-FinanceHub-Provisioner.ps1 (SharePoint & Entra App)" -ForegroundColor White
Write-Host "  3. Run 02-Configure-Azure-Resources.ps1 (Azure provisioning)" -ForegroundColor White
Write-Host ""

# Disconnect
Write-Host "Disconnecting from Exchange Online..." -ForegroundColor Gray
Disconnect-ExchangeOnline -Confirm:$false
Write-Host "  ✓ Disconnected" -ForegroundColor Green
Write-Host ""
