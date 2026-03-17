#!/bin/bash

az logic workflow create \
  --resource-group "rg-financehub-prod" \
  --location "uksouth" \
  --name "logic-financehub-reminders" \
  --definition @workflow.json

echo " Logic App deployed. Configure Office 365 connection in Azure Portal."
