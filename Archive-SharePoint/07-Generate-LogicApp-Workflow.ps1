<#
    07-Generate-LogicApp-Workflow.ps1
    Generates Logic App workflow definition for unpaid invoice reminders
#>

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$configFile = Join-Path $scriptRoot "finance-hub-config.ini"
$outputDir = Join-Path $scriptRoot "LogicApp"

function Get-IniContent {
    param([string]$Path)
    $ini = @{}
    if (-not (Test-Path $Path)) { return $ini }
    $section = $null
    switch -regex -file $Path {
        '^\s*\[(.+)\]\s*$' {
            $section = $matches[1]
            if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} }
        }
        '^\s*([^=]+?)\s*=\s*(.*)$' {
            if (-not $section) { $section = "Default"; if (-not $ini.ContainsKey($section)) { $ini[$section] = @{} } }
            $name = $matches[1].Trim()
            $value = $matches[2]
            $ini[$section][$name] = $value
        }
    }
    return $ini
}

try {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Generating Logic App Workflow Definition" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    if (-not (Test-Path $configFile)) {
        throw "Config file not found. Run 01-Pre-Requisites.ps1 first to configure."
    }
    
    $config = Get-IniContent -Path $configFile
    
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir | Out-Null
    }
    
    Write-Host "Creating Logic App workflow files..." -ForegroundColor Yellow
    Write-Host ""
    
    # Logic App Workflow JSON
    $workflowJson = @"
{
  "`$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "`$connections": {
      "defaultValue": {},
      "type": "Object"
    }
  },
  "triggers": {
    "Recurrence": {
      "recurrence": {
        "frequency": "Day",
        "interval": 2,
        "schedule": {
          "hours": ["9"],
          "minutes": [0]
        },
        "timeZone": "$($config['Regional']['TimeZone'])"
      },
      "type": "Recurrence"
    }
  },
  "actions": {
    "Get_Unpaid_Invoices": {
      "runAfter": {},
      "type": "Http",
      "inputs": {
        "method": "GET",
        "uri": "$($config['FunctionApp']['FunctionAppUrl'])/api/GetUnpaidInvoices?daysOverdue=$($config['LogicApp']['ReminderDaysOverdue'])"
      }
    },
    "Parse_JSON": {
      "runAfter": {
        "Get_Unpaid_Invoices": ["Succeeded"]
      },
      "type": "ParseJson",
      "inputs": {
        "content": "@body('Get_Unpaid_Invoices')",
        "schema": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "integer" },
              "invoiceNumber": { "type": "string" },
              "customerName": { "type": "string" },
              "customerEmail": { "type": "string" },
              "amountGross": { "type": "number" },
              "dateIssued": { "type": "string" },
              "daysOverdue": { "type": "integer" }
            }
          }
        }
      }
    },
    "For_each_Invoice": {
      "foreach": "@body('Parse_JSON')",
      "actions": {
        "Send_Reminder_Email": {
          "runAfter": {},
          "type": "ApiConnection",
          "inputs": {
            "host": {
              "connection": {
                "name": "@parameters('`$connections')['office365']['connectionId']"
              }
            },
            "method": "post",
            "path": "/v2/Mail",
            "body": {
              "To": "@items('For_each_Invoice')['customerEmail']",
              "Cc": "$($config['Email']['ReminderCCEmail'])",
              "From": "$($config['Email']['InvoicesFromEmail'])",
              "Subject": "Payment Reminder: Invoice @{items('For_each_Invoice')['invoiceNumber']}",
              "Body": "<html><body><p>Dear @{items('For_each_Invoice')['customerName']},</p><p>This is a friendly reminder that invoice <strong>@{items('For_each_Invoice')['invoiceNumber']}</strong> is now <strong>@{items('For_each_Invoice')['daysOverdue']} days overdue</strong>.</p><table style='border-collapse: collapse; width: 100%; max-width: 500px;'><tr><td style='padding: 10px; border: 1px solid #ddd;'><strong>Invoice Number:</strong></td><td style='padding: 10px; border: 1px solid #ddd;'>@{items('For_each_Invoice')['invoiceNumber']}</td></tr><tr><td style='padding: 10px; border: 1px solid #ddd;'><strong>Date Issued:</strong></td><td style='padding: 10px; border: 1px solid #ddd;'>@{formatDateTime(items('For_each_Invoice')['dateIssued'], 'dd MMM yyyy')}</td></tr><tr><td style='padding: 10px; border: 1px solid #ddd;'><strong>Amount Due:</strong></td><td style='padding: 10px; border: 1px solid #ddd;'>$($config['Finance']['BaseCurrency']) @{items('For_each_Invoice')['amountGross']}</td></tr></table><p style='margin-top: 20px;'><a href='$($config['StaticWebApp']['StaticWebAppUrl'])/invoice/@{items('For_each_Invoice')['id']}?pay=1' style='background: #3498db; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Mark as Paid</a></p><p style='margin-top: 30px; color: #777; font-size: 0.9em;'>If you have already paid this invoice, please disregard this reminder.</p><p style='color: #777; font-size: 0.9em;'>For any questions, please contact us at $($config['Email']['ReminderCCEmail'])</p></body></html>",
              "Importance": "Normal"
            }
          }
        }
      },
      "runAfter": {
        "Parse_JSON": ["Succeeded"]
      },
      "type": "Foreach"
    }
  },
  "outputs": {}
}
"@
    $workflowJson | Set-Content "$outputDir\workflow.json" -Encoding UTF8
    Write-Host "   workflow.json" -ForegroundColor Green
    
    # PowerShell Deployment Script
    $deployScript = @"
`$resourceGroup = "$($config['Azure']['ResourceGroup'])"
`$logicAppName = "$($config['LogicApp']['LogicAppName'])"
`$workflowFile = "`$PSScriptRoot\workflow.json"

Write-Host "Deploying Logic App workflow..." -ForegroundColor Yellow

az logic workflow create \
  --resource-group `$resourceGroup \
  --location $($config['Azure']['Location']) \
  --name `$logicAppName \
  --definition @"`$workflowFile"

Write-Host " Logic App workflow deployed" -ForegroundColor Green
Write-Host ""
Write-Host "Next: Configure Office 365 connection in Azure Portal"
Write-Host ""
"@
    $deployScript | Set-Content "$outputDir\deploy-logicapp.ps1" -Encoding UTF8
    Write-Host "   deploy-logicapp.ps1" -ForegroundColor Green
    
    # Bash Deployment Script
    $bashDeploy = @"
#!/bin/bash

az logic workflow create \
  --resource-group "$($config['Azure']['ResourceGroup'])" \
  --location "$($config['Azure']['Location'])" \
  --name "$($config['LogicApp']['LogicAppName'])" \
  --definition @workflow.json

echo " Logic App deployed. Configure Office 365 connection in Azure Portal."
"@
    $bashDeploy | Set-Content "$outputDir\deploy-logicapp.sh" -Encoding UTF8
    Write-Host "   deploy-logicapp.sh" -ForegroundColor Green
    
    # ARM Template
    $armTemplate = @"
{
  "`$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "logicAppName": {
      "type": "string",
      "defaultValue": "$($config['LogicApp']['LogicAppName'])"
    },
    "location": {
      "type": "string",
      "defaultValue": "$($config['Azure']['Location'])"
    }
  },
  "resources": [
    {
      "type": "Microsoft.Logic/workflows",
      "apiVersion": "2019-05-01",
      "name": "[parameters('logicAppName')]",
      "location": "[parameters('location')]",
      "properties": {
        "state": "Enabled",
        "definition": {
          "`$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
          "contentVersion": "1.0.0.0",
          "parameters": {},
          "triggers": {
            "Recurrence": {
              "recurrence": {
                "frequency": "Day",
                "interval": 2
              },
              "type": "Recurrence"
            }
          },
          "actions": {}
        }
      }
    }
  ]
}
"@
    $armTemplate | Set-Content "$outputDir\template.json" -Encoding UTF8
    Write-Host "   template.json" -ForegroundColor Green
    
    # README using array
    $readmeLines = @(
        "# Logic App - Unpaid Invoice Reminders"
        ""
        "This Logic App sends automated email reminders for overdue invoices."
        ""
        "## Configuration"
        ""
        "- **Schedule:** Every 2 days at 9:00 AM ($($config['Regional']['TimeZone']))"
        "- **Days Overdue Threshold:** $($config['LogicApp']['ReminderDaysOverdue']) days"
        "- **From Email:** $($config['Email']['InvoicesFromEmail'])"
        "- **CC:** $($config['Email']['ReminderCCEmail'])"
        ""
        "## Deployment"
        ""
        "Run: ``.\deploy-logicapp.ps1`` or ``./deploy-logicapp.sh``"
        ""
        "Then configure Office 365 connection in Azure Portal."
        ""
        "## Workflow"
        ""
        "1. Recurrence trigger (every 2 days at 9am)"
        "2. Get unpaid invoices from Function App"
        "3. Send reminder email for each invoice"
        ""
        "## Support"
        ""
        "Contact: $($config['Email']['ReminderCCEmail'])"
    )
    
    $readmeLines -join "`n" | Set-Content "$outputDir\README.md" -Encoding UTF8
    Write-Host "   README.md" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host " Logic App Workflow Generation Complete!" -ForegroundColor Green
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Files in: $outputDir" -ForegroundColor Yellow
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "ERROR: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}
