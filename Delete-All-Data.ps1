#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deletes all customers and suppliers from the FinanceHub database
.DESCRIPTION
    This script calls the Function App admin endpoint to delete all customers and suppliers.
    It uses Azure AD authentication to get a bearer token.
#>

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Delete All Customers and Suppliers" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$functionAppName = "financehub-func-kemponline"
$functionAppUrl = "https://$functionAppName.azurewebsites.net"
$endpoint = "$functionAppUrl/api/admin/delete-all"

Write-Host "Function App: $functionAppUrl" -ForegroundColor Yellow
Write-Host "Endpoint: $endpoint" -ForegroundColor Yellow
Write-Host ""

# Get access token
Write-Host "Getting access token..." -ForegroundColor Cyan
try {
    $tokenResponse = az account get-access-token --resource "https://$functionAppName.azurewebsites.net" --query accessToken -o tsv
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to get access token"
    }
    Write-Host "✓ Access token obtained" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to get access token: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure you are logged in to Azure CLI:" -ForegroundColor Yellow
    Write-Host "  az login" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "WARNING: This will delete ALL customers and suppliers!" -ForegroundColor Red
Write-Host ""
$confirm = Read-Host "Are you sure you want to continue? (yes/no)"

if ($confirm -ne "yes") {
    Write-Host "Operation cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Calling delete endpoint..." -ForegroundColor Cyan

try {
    $headers = @{
        "Authorization" = "Bearer $tokenResponse"
        "Content-Type" = "application/json"
    }

    $response = Invoke-RestMethod -Uri $endpoint -Method Delete -Headers $headers
    
    Write-Host ""
    Write-Host "✓ Success!" -ForegroundColor Green
    Write-Host "  Customers deleted: $($response.customersDeleted)" -ForegroundColor Green
    Write-Host "  Suppliers deleted: $($response.suppliersDeleted)" -ForegroundColor Green
    Write-Host ""
    Write-Host "Message: $($response.message)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "You can now add fresh customers and suppliers in the app!" -ForegroundColor Cyan
    
} catch {
    Write-Host ""
    Write-Host "✗ Error calling endpoint:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
    exit 1
}
