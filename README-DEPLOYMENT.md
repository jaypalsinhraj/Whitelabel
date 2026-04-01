# Manual deployment steps

This document lists **manual steps to deploy** the Whitelabel stack: containers, PostgreSQL, Azure resources, CI/CD secrets, and verification. It complements:

- **[README.md](README.md)** — architecture, local development, CI/CD overview  
- **[README-MANUAL.md](README-MANUAL.md)** — Entra app registrations, connection strings, database operations, troubleshooting  

---

## 1. What you are deploying

| Piece | Notes |
|-------|--------|
| **Frontend** | Static SPA (nginx) built with **Vite**; production URLs and MSAL settings are **baked in at image build time** (`VITE_*`). |
| **Backend** | ASP.NET Core API; needs **PostgreSQL** (Npgsql) and **Entra** JWT settings at runtime. |
| **Database** | **PostgreSQL** — not created by the included Bicep; you must provision it and point the API at it (see below). |

---

## 2. Prerequisites (before any deployment)

Complete or plan the following:

1. **Microsoft Entra ID** — API and SPA app registrations, API scope, redirect URIs for production URLs. See **[README-MANUAL.md §1](README-MANUAL.md)**.
2. **PostgreSQL** — A server and database the API can reach (Azure Database for PostgreSQL, RDS, managed instance, or a VM). Note **host**, **database name**, **user**, **password** (or managed identity / AAD auth if you configure it in the app).
3. **Secrets** — Do not commit production connection strings or Entra secrets to git. Use **Azure Key Vault references**, **Container Apps secrets**, or **GitHub Actions secrets** as appropriate.

---

## 3. Deploy with Docker Compose (manual)

From the repository root (after Docker is installed):

1. Ensure **`frontend/.env`** contains the values you want baked into a local image (or rely on defaults for smoke tests).
2. Start the stack:

   ```bash
   docker compose build
   docker compose up -d
   ```

3. **PostgreSQL** starts with the compose file; the backend waits for it to be healthy, then runs migrations and seed on first start.
4. Open the frontend (e.g. `http://localhost:3000`) and confirm the API (e.g. `http://localhost:5000/health`).

To **tear down** and remove Postgres data:

```bash
docker compose down -v
```

(`-v` removes the named volume; destructive.)

---

## 4. Deploy to Azure (manual outline)

The repo includes **Bicep** (`infra/main.bicep`) and **GitHub Actions** (`.github/workflows/ci-cd.yml`). A fully manual path looks like this:

### 4.1 Build and push images

1. Create or use an **Azure Container Registry** (ACR).
2. Log in: `az acr login --name <acrName>`
3. Build and push **frontend** with production **`VITE_*`** build args (authority, redirect URI = **HTTPS** frontend FQDN, API base URL = **HTTPS** backend FQDN, scope, SPA client ID). Example:

   ```bash
   docker build ./frontend \
     -t <acr>.azurecr.io/whitelabel-frontend:<tag> \
     --build-arg VITE_MSAL_CLIENT_ID=... \
     --build-arg VITE_MSAL_AUTHORITY=... \
     --build-arg VITE_MSAL_REDIRECT_URI=https://<frontend-host>/ \
     --build-arg VITE_API_SCOPE=... \
     --build-arg VITE_API_BASE_URL=https://<backend-host>/
   docker push <acr>.azurecr.io/whitelabel-frontend:<tag>
   ```

4. Build and push **backend** (no `VITE_*`):

   ```bash
   docker build ./backend -t <acr>.azurecr.io/whitelabel-backend:<tag>
   docker push <acr>.azurecr.io/whitelabel-backend:<tag>
   ```

### 4.2 Provision PostgreSQL (manual)

The Bicep template **does not** create PostgreSQL. In Azure Portal or CLI:

1. Create **Azure Database for PostgreSQL** (Flexible Server recommended).
2. Create database (e.g. `whitelabel`), user/password or Entra ID auth per your policy.
3. Allow network access from **Azure** (or your Container Apps subnet / VNet integration) per your security model.
4. Build the **Npgsql** connection string, e.g.:

   `Host=<server>.postgres.database.azure.com;Database=whitelabel;Username=...;Password=...;Ssl Mode=Require;Trust Server Certificate=true`

### 4.3 Deploy infrastructure (Bicep)

1. Copy and edit **`infra/params/dev.bicepparam`** (or create `prod.bicepparam`) — unique **`acrName`**, environment names, etc.
2. Deploy:

   ```bash
   az deployment group create \
     --resource-group <your-rg> \
     --template-file infra/main.bicep \
     --parameters @infra/params/dev.bicepparam \
     --parameters acrName=<unique-acr> \
     --parameters frontendImageTag=<tag> \
     --parameters backendImageTag=<tag> \
     --parameters azureAdTenantId='<guid>' \
     --parameters azureAdApiClientId='<api-client-id>' \
     --parameters azureAdAudience='api://<api-client-id>' \
     --parameters azureAdDomain='<optional-domain>'
   ```

3. Note outputs **`frontendFqdn`** and **`backendFqdn`**.

### 4.4 Configure the backend Container App (required manual step)

The Bicep module sets **Entra** and **CORS** for the backend, but **not** the database connection string. You must add it:

1. Azure Portal → **Container Apps** → your API app → **Containers** → **Environment variables** (or **Secrets** + reference).
2. Set:

   - `ConnectionStrings__DefaultConnection` = your PostgreSQL connection string (store as a **secret** in production).
   - Optionally `ASPNETCORE_ENVIRONMENT` = `Production` (often already set by Bicep).

3. Ensure **CORS** `Cors__Origins__0` matches the **HTTPS** frontend URL after you know the real FQDN (Bicep sets it from deployment; if the frontend FQDN was empty at deploy time, fix CORS manually).

4. **Revision** must restart so the API runs migrations against PostgreSQL.

---

## 5. GitHub Actions (manual secret setup)

The workflow **`.github/workflows/ci-cd.yml`** builds and deploys on push to **`main`** when configuration is present.

### 5.1 Secrets (keep these — OIDC)

Only these need to stay under **Settings → Secrets and variables → Actions → Secrets**:

| Secret | Purpose |
|--------|---------|
| `AZURE_CLIENT_ID` | Entra app for GitHub Actions OIDC |
| `AZURE_TENANT_ID` | Azure AD tenant |
| `AZURE_SUBSCRIPTION_ID` | Subscription to deploy into |

### 5.2 Variables (recommended — no secrets clutter)

The deploy job reads **Repository variables** first, then falls back to **secrets** with the same name (for backward compatibility).

Under **Settings → Secrets and variables → Actions → Variables**, create:

| Variable | Purpose |
|----------|---------|
| `ACR_NAME` | Short ACR name (no `.azurecr.io`) |
| `AZURE_RESOURCE_GROUP` | Resource group name |
| `VITE_MSAL_CLIENT_ID` | SPA client ID (public in the browser) |
| `VITE_MSAL_AUTHORITY` | Authority URL |
| `VITE_MSAL_REDIRECT_URI` | Production SPA URL (`https://…`) |
| `VITE_API_SCOPE` | API scope |
| `VITE_API_BASE_URL` | Public HTTPS backend URL |
| `AZURE_AD_TENANT_ID` | Platform tenant ID for the API container |
| `AZURE_AD_API_CLIENT_ID` | API app client ID |
| `AZURE_AD_AUDIENCE` | Token audience |
| `AZURE_AD_DOMAIN` | Optional — `AzureAd:Domain` |

SPAs expose the client ID and authority in the bundle; API URLs are not secret credentials — **variables** are appropriate and avoid maintaining a long list of secrets.

### 5.3 Other steps

1. **Azure OIDC federated credential** — Register an Entra app for GitHub Actions, add a federated credential for `repo:<org>/<repo>:ref:refs/heads/main`, and grant subscription/RG permissions. See Azure docs: *OpenID Connect with GitHub Actions*.
2. **Database** — The workflow does **not** inject `ConnectionStrings__DefaultConnection`. After the first deploy, set the PostgreSQL connection string on the backend Container App **manually** (or extend Bicep / workflow).

See **[README.md § CI/CD](README.md#cicd-pipeline-github-actions)** for the summary table.

---

## 6. Post-deployment verification

| Step | Action |
|------|--------|
| Health | `GET https://<backend-fqdn>/health` and `/health/ready` — expect **200**. |
| CORS | Browser must be allowed to call the API from the SPA origin; fix `Cors__Origins__*` if the UI shows CORS errors. |
| Auth | Sign in via SPA; call **`GET /secure-data`** with a bearer token. |
| Database | Confirm tables exist after first API start (migrations run on startup). |

---

## 7. Updating a live deployment

1. **Frontend** — Rebuild image with updated **`VITE_*`** if URLs or Entra IDs changed; redeploy the Container App revision.
2. **Backend** — Rebuild/push image; new revision pulls the image. Run **EF migrations** by deploying a version that includes new migrations (API applies them on startup).
3. **PostgreSQL** — Plan maintenance windows for major version upgrades; back up before schema changes.

---

## 8. Rollback

- **Container Apps**: Activate a previous **revision** or redeploy an older image tag.
- **Database**: Restore from backup if a migration failed; avoid deleting production volumes without backups.

---

*For Entra details, Npgsql connection formats, and operational troubleshooting, see **[README-MANUAL.md](README-MANUAL.md)**.*
