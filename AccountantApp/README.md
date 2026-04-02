# Finlytics Accountant Portal

A read-only portal for external accountants to view a client company's financial data.

## Stack
- **React + Vite** — SPA, deployed as an Azure Static Web App
- **Clerk** — accountant authentication (email magic-link / OTP)
- **Azure Functions backend** — existing `/api/accountant/*` endpoints

## Local development

```bash
npm install
npm run dev         # starts on http://localhost:5174
```

You'll need a `.env.local` with:

```
VITE_CLERK_PUBLISHABLE_KEY=pk_test_xxxx
VITE_API_BASE=https://financehub-func-kemponline.azurewebsites.net/api
```

## Invite flow

1. Owner opens **Settings → Accountants** in the main Finlytics portal and clicks **Invite Accountant**.
2. They enter the accountant's email. The API sends a one-time invite email with a link like:  
   `https://<accountant-portal>/?token=<inviteToken>`
3. Accountant clicks the link → lands on the portal → signs in / creates a Clerk account.
4. On first sign-in the portal calls `POST /api/accountant/accept-invite` with `{ inviteToken }` and the Clerk JWT.  
   Backend links the Clerk user ID to the company access record.
5. Subsequent visits: Clerk JWT is verified on every API call; accountant sees read-only company data.

## Deployment

```bash
npm run build
# then deploy ./dist to Azure Static Web Apps
```

Set these application settings on the SWA / Function App:
- `ACCOUNTANT_PORTAL_URL` — URL of this accountant portal (so invite emails link here)
- `CLERK_SECRET_KEY` — Clerk backend secret (already used by `ClerkAuthService`)
