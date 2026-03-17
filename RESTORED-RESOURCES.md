# Finance Hub - Restored Resources

**Date:** February 8, 2026  
**Resource Group:** rg-financehub-prod-v2 (temporary - will rename to rg-financehub-prod once old RG deletion completes)

## ✅ Recreated Resources

### Function App
- **Name:** func-financehub-2669
- **URL:** https://func-financehub-2669.azurewebsites.net
- **Status:** ✅ Deployed and Running
- **Managed Identity:** 577781eb-eeb9-42a5-bf51-1c9d8e5d48dc

### Static Web App
- **Name:** swa-financehub-2669
- **URL:** https://lemon-field-0e8af4103.2.azurestaticapps.net
- **Status:** ✅ Deployed
- **Deployment Token:** (stored separately)

### Storage Account
- **Name:** stfinancehub2669
- **Status:** ✅ Created
- **Endpoints:**
  - Blob: https://stfinancehub2669.blob.core.windows.net/
  - Table: https://stfinancehub2669.table.core.windows.net/
  - Queue: https://stfinancehub2669.queue.core.windows.net/

### App Service Plan
- **Name:** asp-financehub-prod
- **SKU:** B1 (Basic)
- **OS:** Linux

### Application Insights
- **Name:** func-financehub-2669
- **Status:** ✅ Auto-created

## ⚠️ Still Pending

### Key Vault
- **Original Name:** kv-financehub-kempy
- **Status:** In soft-delete state (needs recovery or new name)
- **Action Needed:** Once rg-financehub-prod deletion completes, recover or create new vault

### Logic App
- **Original Name:** logic-financehub-reminders
- **Status:** Not yet recreated
- **Action Needed:** Recreate if needed

## 📋 Next Steps

1. **Wait for original RG deletion to complete** (~10-15 minutes)
   - Run: `az group exists --name rg-financehub-prod`
   - When it returns `false`, proceed to recover Key Vault

2. **Recover Key Vault**
   ```bash
   az keyvault recover --name kv-financehub-kempy
   # OR create with new name if secrets not needed
   ```

3. **Update Function App Settings**
   - Add any required connection strings/secrets
   - Configure SharePoint settings if needed

4. **Update Entra App Registration**
   - Add new Static Web App URL redirect URI:
     `https://lemon-field-0e8af4103.2.azurestaticapps.net/.auth/login/aad/callback`

5. **Test the Application**
   - Verify Static Web App loads
   - Test authentication
   - Verify API calls to Function App work
   - Check SharePoint data access

## 📝 Important Notes

- SharePoint data is intact (stored in M365)
- Function App code successfully deployed
- Static Web App successfully deployed
- All resources in new RG: `rg-financehub-prod-v2`
- Original RG `rg-financehub-prod` is being deleted

## 🔄 Resource Group Rename

Once the old RG deletion completes:
1. Option A: Rename the new RG (not directly supported - need to move resources)
2. Option B: Keep `rg-financehub-prod-v2` as the prod RG name
3. Option C: Export resources, delete v2, recreate as rg-financehub-prod

**Recommendation:** Keep rg-financehub-prod-v2 name for simplicity.
