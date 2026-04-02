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
| **Database** | **Azure Database for PostgreSQL (Flexible Server)** — created by **`infra/main.bicep`** (database `whitelabel`). The backend Container App receives **`ConnectionStrings__DefaultConnection`** as a **secret** from Bicep. You supply a strong **`postgresAdminPassword`** at deploy time (GitHub secret or env var for CLI). |

---

## 2. Prerequisites (before any deployment)

Complete or plan the following:

1. **Microsoft Entra ID** — API and SPA app registrations, API scope, redirect URIs for production URLs. See **[README-MANUAL.md §1](README-MANUAL.md)**.
2. **PostgreSQL (Azure)** — For **Bicep / GitHub Actions**, choose a strong **`POSTGRES_ADMIN_PASSWORD`** (stored as a GitHub **secret** or in `POSTGRES_ADMIN_PASSWORD` env when using `az` with `.bicepparam`). For **Docker Compose** only, Postgres runs in Compose and does not use this.
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

### 4.2 PostgreSQL in Azure (Bicep)

`infra/modules/postgresFlexible.bicep` deploys a **Flexible Server** (Burstable **Standard_B1ms**, PostgreSQL **16**, 32 GiB storage), database **`whitelabel`**, and a firewall rule so **Azure services** (including Container Apps) can connect. The API app gets **`ConnectionStrings__DefaultConnection`** and **`Database__Provider=PostgreSQL`** via Container Apps **secrets**.

You must pass **`postgresAdminPassword`** (secure) on every deployment. **GitHub Actions** uses the **`POSTGRES_ADMIN_PASSWORD`** repository secret. For **CLI** with `dev.bicepparam`, set `export POSTGRES_ADMIN_PASSWORD='...'` before deploy (see `readEnvironmentVariable` in that file).

For production, consider **VNet integration**, **private access**, larger SKUs, and longer backups — adjust `postgresFlexible.bicep` as needed.

### 4.3 Deploy infrastructure (Bicep)

1. Copy and edit **`infra/params/dev.bicepparam`** (or create `prod.bicepparam`) — unique **`acrName`**, environment names, etc.
2. Deploy using **either** a single `.bicepparam` file **or** inline `--parameters` for every required parameter — **do not** combine `--parameters @file.bicepparam` with extra `--parameters` flags; Azure CLI can mis-parse that and fail.

   **Option A — param file only** (put all values, including `acrName` and Entra IDs, in the file first):

   ```bash
   az deployment group create \
     --resource-group <your-rg> \
     --template-file infra/main.bicep \
     --parameters @infra/params/dev.bicepparam
   ```

   **Option B — inline only** (matches the GitHub Actions deploy job):

   ```bash
   az deployment group create \
     --resource-group <your-rg> \
     --template-file infra/main.bicep \
     --parameters environment=dev \
     --parameters containerAppsEnvName=cae-whitelabel-dev \
     --parameters logRetentionDays=30 \
     --parameters minReplicas=1 \
     --parameters maxReplicas=3 \
     --parameters acrName=<unique-acr> \
     --parameters frontendImageTag=<tag> \
     --parameters backendImageTag=<tag> \
     --parameters azureAdTenantId='<guid>' \
     --parameters azureAdApiClientId='<api-client-id>' \
     --parameters azureAdAudience='api://<api-client-id>' \
     --parameters azureAdDomain='<optional-domain>' \
     --parameters postgresAdminLogin=whitelabel \
     --parameters postgresAdminPassword='<strong-password>'
   ```

3. Note outputs **`frontendFqdn`**, **`backendFqdn`**, and **`postgresFqdn`**.

### 4.4 Optional backend tweaks

Bicep already sets **Entra**, **CORS**, **PostgreSQL connection string** (secret), and **`ASPNETCORE_ENVIRONMENT=Production`**. If you change URLs or use an **external** database later, update the Container App’s env/secrets in the portal or extend Bicep.

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
| `POSTGRES_ADMIN_PASSWORD` | Strong password for the **Azure PostgreSQL flexible server** admin user (also used in the API connection string). |

### 5.2 Variables (recommended — no secrets clutter)

The deploy job reads **Repository variables** first, then falls back to **secrets** with the same name (for backward compatibility).

Under **Settings → Secrets and variables → Actions → Variables**, create:

| Variable | Purpose |
|----------|---------|
| `ACR_NAME` | Registry name only: **lowercase letters and digits**, 5–50 chars, **globally unique** in Azure (e.g. `acrwhitelabeldev001`). The workflow lowercases the value, **creates the ACR** in your RG if it does not exist, then pushes images **before** Bicep deploy. Must match the subscription used by `AZURE_SUBSCRIPTION_ID`. |
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
2. **Database** — Bicep creates PostgreSQL and injects **`ConnectionStrings__DefaultConnection`** into the backend Container App; the workflow must supply **`POSTGRES_ADMIN_PASSWORD`** (secret above).

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
