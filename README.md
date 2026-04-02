# White-Label Multi-Tenant SaaS (Starter)

## Overview

**Manual steps** (Entra app registrations, databases, environment variables, production checklist) are in **[README-MANUAL.md](README-MANUAL.md)**. **Manual deployment** (Docker Compose, Azure images, Bicep, CI secrets, PostgreSQL wiring) is in **[README-DEPLOYMENT.md](README-DEPLOYMENT.md)**.

This repository is a **production-style starter** for a **single-deployment, multi-tenant, white-label** SaaS product on **Microsoft Azure**. It includes:

- **React + Vite + TypeScript** SPA (port **3000**) with **MSAL** (Authorization Code + PKCE), protected routes, and dynamic tenant branding.
- **ASP.NET Core 8** Web API (port **5000**) with **Microsoft.Identity.Web** JWT validation, tenant resolution middleware, and sample endpoints.
- **Docker** images and **docker-compose** for local full-stack development.
- **Bicep** infrastructure for **Azure Container Registry**, **Log Analytics**, **Container Apps Environment**, and two **Container Apps** (frontend + backend).
- **GitHub Actions** CI/CD: build, containerize, push to ACR, deploy with Bicep.

**Tenants** share one deployment; **branding** (colors, logo, name) and routing metadata are resolved per request. **Authentication** follows a **Microsoft Entra ID B2B** style model: the SaaS vendor hosts app registrations in a **platform tenant**; customer organizations sign in with their own Entra tenants and are represented as **guest users** in the platform directory.

---

## Solution architecture

```text
┌─────────────────────────────────────────────────────────────────┐
│                        Microsoft Entra ID                        │
│  Platform tenant: app registrations (SPA + API)                  │
│  Customer tenants: users sign in at home IdP; B2B guests in hub   │
└───────────────────────────┬─────────────────────────────────────┘
                            │ OAuth2 / OIDC (Auth Code + PKCE)
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│   React SPA (MSAL)  ──Bearer token──►  ASP.NET Core API (MIW)    │
│   • Tenant branding from GET /tenant                             │
│   • Protected UI + API calls with access token                   │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Azure (optional): ACR + Container Apps + Log Analytics          │
│  Bicep provisions registry, environment, and two apps          │
└─────────────────────────────────────────────────────────────────┘
```

**Tenant resolution (backend)** evaluates, in order:

1. `X-Tenant-Id` header (explicit override, useful for dev/testing).
2. **Host name** mapping (`HostNames` per tenant in configuration).
3. **`Domain`** field match against the HTTP host.
4. **`DefaultTenantId`** fallback.

**User-to-tenant mapping** (for `/secure-data` illustration) uses:

- Entra **`tid`** claim vs configured `EntraTenantId`, then
- **Email / `preferred_username` domain** vs `EmailDomains` / `Domain`.

---

## Repository structure

```text
Whitelabel/
├── README.md
├── README-MANUAL.md     # Entra, DB, env vars, operations
├── README-DEPLOYMENT.md # Manual deploy: Compose, Azure, CI, Postgres
├── .gitignore
├── docker-compose.yml
├── frontend/
│   ├── Dockerfile
│   ├── nginx.conf
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── tsconfig.node.json
│   ├── index.html
│   ├── .env.example
│   └── src/
│       ├── main.tsx
│       ├── App.tsx
│       ├── vite-env.d.ts
│       ├── auth/          # MSAL config + provider
│       ├── components/    # ProtectedRoute
│       ├── pages/         # Login, Dashboard, Admin
│       ├── services/      # API client + bearer token
│       ├── tenant/        # Tenant context + types
│       └── theme/         # Branding theme wrapper
├── backend/
│   ├── Dockerfile
│   ├── Whitelabel.sln
│   └── src/
│       ├── Whitelabel.Api/
│       ├── Whitelabel.Application/
│       ├── Whitelabel.Domain/
│       └── Whitelabel.Infrastructure/   # EF Core, TenantCatalog, migrations
└── infra/
    ├── main.bicep
    ├── modules/
    │   ├── acr.bicep
    │   ├── loganalytics.bicep
    │   ├── containerAppsEnv.bicep
    │   └── containerApp.bicep
    └── params/
        ├── dev.bicepparam
        └── prod.bicepparam
├── .github/
│   └── workflows/
│       └── ci-cd.yml
```

---

## Multi-tenant design

- **Single deployment** serves multiple logical tenants.
- Each tenant defines: `tenantId`, `tenantName`, `primaryColor`, `secondaryColor`, `logoUrl`, `domain`, `EntraTenantId`, plus optional `HostNames` and `EmailDomains` for routing and claim mapping.
- **Two sample tenants** are preconfigured: **contoso** and **fabrikam** (see `backend/src/Whitelabel.Api/appsettings.json`).
- The SPA calls **`GET /tenant`** to paint branding before or after sign-in; optional `VITE_TENANT_ID` sends `X-Tenant-Id` for local testing.

---

## Roles and access control (platform admin, tenant admin, tenant user)

The starter uses **Microsoft Entra object IDs (`oid`)** from the JWT to decide who can use admin APIs. There is no separate login—users sign in with Entra; the API reads claims and compares `oid` to configuration.

### How the API identifies a user

| Claim | Typical use |
|-------|-------------|
| **`oid`** | Stable Entra object ID for the user (primary key for admin lists). Also available as `http://schemas.microsoft.com/identity/claims/objectidentifier` in some tokens. |
| **`tid`** | Directory (tenant) ID for the token issuer; used to map a user to a **logical SaaS tenant** when it matches `EntraTenantId` in tenant config. |
| **`email` / `preferred_username`** | Used as a fallback to map users to a tenant by **email domain** vs `EmailDomains` / `Domain`. |

Put your real **`oid`** values in `appsettings.json` (or override via environment variables). You can read `oid` from **`GET /secure-data`** in the JSON `claims` section after signing in, or decode an access token at [jwt.ms](https://jwt.ms).

### Platform (application) admin

- **Definition:** A user whose `oid` appears in **`Admin:ApplicationAdminObjectIds`** (`backend/src/Whitelabel.Api/appsettings.json`).
- **Can:** Create tenants (`POST /admin/tenants`), list tenants (`GET /admin/tenants`), and perform any tenant-admin action on any tenant (branding update, grant user access, list users for a tenant).

Example (replace with your `oid`):

```json
"Admin": {
  "ApplicationAdminObjectIds": [ "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" ]
}
```

### Tenant admin

- **Definition:** A user whose `oid` appears in that tenant’s **`TenantAdminObjectIds`** array under **`Tenants:Items`** for the matching `tenantId`.
- **Can:** Update that tenant’s white-label fields (`PUT /admin/tenants/{tenantId}/branding`), grant access to tenant users (`POST /admin/tenants/{tenantId}/users`), list users (`GET /admin/tenants/{tenantId}/users`).

Example fragment:

```json
{
  "TenantId": "contoso",
  "TenantAdminObjectIds": [ "11111111-2222-3333-4444-555555555555" ]
}
```

### Tenant user (normal user)

- **Mapping to a tenant:** The API resolves which **SaaS tenant** applies using:
  1. **`tid`** claim vs tenant `EntraTenantId`, or  
  2. Email / UPN **domain** vs `EmailDomains` and `Domain`.
- **Access to protected API data:** In addition to being “mapped” to a tenant, **`GET /secure-data`** allows access if the user is a **platform admin**, a **tenant admin** for that tenant, **mapped to the tenant** by claims, or **explicitly granted** via `POST /admin/tenants/{tenantId}/users` (body: `{ "userObjectId": "<oid>" }`).

### Admin UI

- **`/admin`** (authenticated): **Application admins** see **Create tenant**, **Update current tenant branding**, and **Grant tenant access**. **Tenant admins** (without application admin) only see **Update current tenant branding**. All actions still enforce the same `oid` checks on the API.
- **`/dashboard`**: shows a link to the admin console when **`GET /secure-data`** returns `roles.isApplicationAdmin` or `roles.isTenantAdmin`.

### API endpoints (summary)

| Endpoint | Who |
|----------|-----|
| `GET /admin/tenants` | Platform admin |
| `POST /admin/tenants` | Platform admin |
| `PUT /admin/tenants/{tenantId}/branding` | Platform admin or tenant admin for `{tenantId}` |
| `POST /admin/tenants/{tenantId}/users` | Platform admin or tenant admin |
| `GET /admin/tenants/{tenantId}/users` | Platform admin or tenant admin |
| `GET /health` | Anonymous; liveness |
| `GET /health/ready` | Anonymous; readiness (includes database) |

### Persistence and roles

- **Database:** Tenant data, grants, and admin assignments are stored with **EF Core** and **PostgreSQL** (Npgsql). Migrations run **on startup**. An **initial seed** runs when the database is empty, using `Tenants:Items` and `Admin:ApplicationAdminObjectIds` in configuration. **Azure:** Bicep deploys **Azure Database for PostgreSQL (Flexible Server)** and passes the connection string to the API Container App. See **[README-MANUAL.md](README-MANUAL.md)** for connection strings and operations.
- **Platform admins:** Recognized via `Admin:ApplicationAdminObjectIds` **and** rows in the **`ApplicationAdmins`** table (config OIDs are merged on startup). Prefer **Key Vault** or **Container App** secrets for production, not committed `appsettings`.
- **Further hardening:** For larger deployments, consider **Entra app roles** or **groups** in the token instead of OID lists. **`TenantResolution:AllowTenantIdHeader`** is **`false`** in `appsettings.Production.json` so clients cannot pick a tenant via `X-Tenant-Id` unless you override.

### Troubleshooting: “I updated appsettings but nothing changed”

1. **Restart the API** after configuration changes. **Tenant seed data** from `Tenants:Items` only applies when the database was **empty** on first run; later edits require the **Admin API** or direct DB updates. **Application admin OIDs** in `Admin:ApplicationAdminObjectIds` are merged into the database on each startup (new OIDs are added). With Docker: rebuild/restart so the API reloads config and environment variables.
2. **Confirm the `oid` matches the signed-in user.** Use **`GET /secure-data`** → `claims.oid` or decode the access token. The value must match **`Admin:ApplicationAdminObjectIds`** or **`TenantAdminObjectIds`** exactly (GUID comparison is case-insensitive).
3. **Tenant admin + localhost:** Tenant admin is recognized for the **resolved branding tenant** (`resolvedBrandingTenantId` from `/secure-data`) *and* the claim-mapped tenant. If you use `http://localhost:3000`, ensure the tenant you see in the UI has **`HostNames` including `"localhost"`** (sample **contoso** does) and put your **`oid`** under that tenant’s **`TenantAdminObjectIds`**. If you only list your `oid` under **fabrikam** but the UI resolves **contoso**, you will not show as that tenant’s admin until you align config or use the correct host/header.
4. **Claim mapping vs real directory:** If token **`tid`** is your real Entra tenant GUID but **`Tenants:Items[].EntraTenantId`** is still a placeholder (e.g. `aaaaaaaa-...`), `userMappedTenantId` may be **null**. That is OK for **tenant admin** detection **as long as** your `oid` is an admin for the **resolved** tenant (see point 3). For mapping-based features, set **`EntraTenantId`** and **`EmailDomains`** to real values for your tests.

---

## Authentication with Entra ID (B2B-style)

### Platform vs customer tenants

- **Platform tenant** holds the **API** and **SPA** app registrations.
- **Customer organizations** use their own Entra tenants at sign-in; after invitation/redemption, users exist as **B2B guests** in the platform tenant for authorization and auditing scenarios.

### Frontend (MSAL)

- **Packages**: `@azure/msal-browser`, `@azure/msal-react`.
- **Flow**: SPA **Authorization Code** with **PKCE**; `loginRedirect` / `acquireTokenSilent` / `acquireTokenRedirect`.
- **Scopes**: OpenID scopes for sign-in; **API scope** (e.g. `api://<api-app-id>/access_as_user`) for the backend.
- **Bootstrap**: `PublicClientApplication.initialize()` and `handleRedirectPromise()` in `main.tsx`.

### Backend (Microsoft.Identity.Web)

- **JWT Bearer** authentication; validates **issuer** and **audience** per `AzureAd` in configuration.
- **Claims** surfaced in sample payload: `tid`, `oid`, `email`, `preferred_username`.

### Configure `AzureAd` (API)

Set in `appsettings.json`, environment variables, or Container App settings:

| Key | Purpose |
|-----|---------|
| `Instance` | `https://login.microsoftonline.com/` |
| `TenantId` | Platform (hub) tenant ID |
| `ClientId` | **API** app registration (application ID) |
| `Audience` | Often `api://<api-client-id>` or App ID URI |
| `Domain` | Optional directory domain hint |

Environment variable names use `__` nesting (e.g. `AzureAd__Audience`).

---

## Local development setup

### Prerequisites

- **Node.js 20+**, **.NET SDK 8**, optional **Docker Desktop**.
- **Microsoft Entra ID** app registrations (see [Manual Azure setup](#manual-azure-setup-step-by-step)).

### Backend

```bash
cd backend
dotnet run --project src/Whitelabel.Api
```

API: `http://localhost:5000`, Swagger: `/swagger`.

### Frontend

```bash
cd frontend
copy .env.example .env   # Windows; use cp on macOS/Linux
# Edit .env with your tenant IDs, client IDs, API scope, and API URL
npm install
npm run dev
```

SPA: `http://localhost:3000`.

### Entra configuration in `.env` (frontend)

| Variable | Description |
|----------|-------------|
| `VITE_MSAL_CLIENT_ID` | SPA app (client) ID |
| `VITE_MSAL_AUTHORITY` | e.g. `https://login.microsoftonline.com/<platform-tenant-id>` |
| `VITE_MSAL_REDIRECT_URI` | `http://localhost:3000` |
| `VITE_API_SCOPE` | Exposed API scope the SPA should request |
| `VITE_API_BASE_URL` | `http://localhost:5000` |
| `VITE_TENANT_ID` | Optional; sends `X-Tenant-Id` |

Match **`AzureAd:Audience`** and **`AzureAd:ClientId`** on the API with the **exposed API** application.

---

## Docker usage

From the repository root:

```bash
docker compose build
docker compose up
```

- Frontend: `http://localhost:3000`
- Backend: `http://localhost:5000`

Pass Entra-related environment variables for the API (see `docker-compose.yml`). For the **frontend image**, MSAL and API URLs are **build-time** (`Dockerfile` `ARG`/`ENV`); set `VITE_*` when building for each environment.

**Note:** Browsers call the API using `VITE_API_BASE_URL`. For local compose, `http://localhost:5000` is correct from the host browser.

---

## Azure infrastructure (Bicep)

**Scope:** resource group (`targetScope = 'resourceGroup'`).

**Modules:**

- **ACR** — stores container images; admin user enabled for template simplicity (prefer managed identity in production hardening).
- **Log Analytics** — workspace + keys for Container Apps diagnostics.
- **Container Apps Environment** — shared environment for apps.
- **Container Apps** — `whitelabel-frontend` and `whitelabel-backend` images from ACR.

**Parameters** (see `infra/params/*.bicepparam`):

- `environment`, `acrName`, `containerAppsEnvName`, image tags, scaling, optional Entra IDs for API env injection.

**Deploy example:**

```bash
az group create -n rg-whitelabel-dev -l uksouth
# Edit infra/params/dev.bicepparam (acrName, tags, Entra IDs), then deploy with the file only — do not add extra --parameters.
az deployment group create \
  --resource-group rg-whitelabel-dev \
  --template-file infra/main.bicep \
  --parameters @infra/params/dev.bicepparam
```

**Outputs:** `frontendFqdn`, `backendFqdn`, `acrLoginServerOut`, `logAnalyticsWorkspaceId`.

**SPA note:** Vite env vars are baked into the static bundle at **image build** time. Set `VITE_API_BASE_URL` to the **public HTTPS URL** of the backend Container App when building the frontend image for Azure.

---

## CI/CD pipeline (GitHub Actions)

Workflow: `.github/workflows/ci-cd.yml`.

- **CI (all PRs / pushes):** `npm ci` + `npm run build` (with dummy MSAL env), `dotnet build`.
- **CD (pushes to `main` only):** Azure OIDC login → ensure ACR → build/push images → `az deployment group create` with `infra/main.bicep` and **inline `--parameters`** (same values as `infra/params/dev.bicepparam`; do not mix `@*.bicepparam` with extra `--parameters` in Azure CLI).

### GitHub configuration (secrets vs variables)

**Keep as Repository secrets** (sensitive — OIDC to Azure and database admin):

| Secret | Purpose |
|--------|---------|
| `AZURE_CLIENT_ID` | Entra app (workload identity) used with OIDC `azure/login` |
| `AZURE_TENANT_ID` | Azure AD tenant |
| `AZURE_SUBSCRIPTION_ID` | Target subscription |
| `POSTGRES_ADMIN_PASSWORD` | Password for the **Azure PostgreSQL Flexible Server** admin account created by Bicep (embedded in the API connection string). |

**Use Repository variables** (recommended — not secret; visible to repo admins): **Settings → Secrets and variables → Actions → Variables** — same names as below. The workflow reads **`vars.<NAME>`** first, then falls back to **`secrets.<NAME>`** if you still store values as secrets.

| Variable (or secret) | Purpose |
|---------------------|---------|
| `ACR_NAME` | ACR name: **lowercase** alphanumeric, globally unique (see [README-DEPLOYMENT.md](README-DEPLOYMENT.md)) |
| `AZURE_RESOURCE_GROUP` | Resource group for deployment |
| `VITE_MSAL_CLIENT_ID` | SPA client ID (public in the browser anyway) |
| `VITE_MSAL_AUTHORITY` | Authority URL |
| `VITE_MSAL_REDIRECT_URI` | Production SPA URL (`https://…`) |
| `VITE_API_SCOPE` | API scope string |
| `VITE_API_BASE_URL` | Public backend URL (`https://…`) |
| `AZURE_AD_TENANT_ID` | Platform tenant ID for API container |
| `AZURE_AD_API_CLIENT_ID` | API app registration client ID |
| `AZURE_AD_AUDIENCE` | Expected token audience |
| `AZURE_AD_DOMAIN` | Optional — `AzureAd:Domain` |

Configure **federated credentials** on the Entra app so `azure/login` can authenticate via OIDC (see below). More detail: **[README-DEPLOYMENT.md](README-DEPLOYMENT.md#5-github-actions-manual-secret-setup)**.

---

## Security considerations

- **Admin RBAC:** Platform and tenant admins are identified by Entra **`oid`** in configuration—see [Roles and access control](#roles-and-access-control-platform-admin-tenant-admin-tenant-user). Treat `appsettings.json` with real OIDs as sensitive.
- **Secrets:** Never commit `.env`, client secrets, or ACR passwords. Prefer **OIDC** for GitHub→Azure, **Key Vault** for runtime secrets, and **managed identities** for ACR pull in production templates.
- **CORS:** Restrict `Cors:Origins` to known SPA origins only.
- **Tokens:** API validates issuer/audience; scope checks can be added via `[Authorize(Policy = "...")]`.
- **Tenant headers:** `X-Tenant-Id` is powerful; in production, restrict who can set it (or remove in favor of host-only resolution).
- **Dependency advisories:** Run `dotnet list package --vulnerable` and `npm audit`; upgrade packages regularly.
- **Container Apps:** Use HTTPS-only ingress, private endpoints, and WAF as your threat model requires.

---

## Operations and monitoring

- **Log Analytics:** Workspace created by Bicep; link Container Apps diagnostics in Azure Portal.
- **Container Apps:** Metrics for CPU/memory/replicas; configure alerts on HTTP errors and latency.
- **ACR:** Scan images (Defender for Cloud) and tag immutable releases by Git SHA (workflow uses `${{ github.sha }}`).

---

## Manual Azure setup (step-by-step)

### 1. Create backend API app registration

1. Entra admin center → **App registrations** → **New registration**.
2. Name: `Whitelabel API`, account type: **Single tenant** (platform tenant).
3. Register → note **Application (client) ID**.

### 2. Expose API scopes

1. Open the API registration → **Expose an API** → **Add** Application ID URI → e.g. `api://<api-client-id>`.
2. **Add a scope** → e.g. `access_as_user`, admins and users consent.

### 3. Create React SPA app registration

1. **New registration** → Name: `Whitelabel SPA`, single tenant.
2. Note **Application (client) ID**.

### 4. Redirect URIs (SPA)

1. SPA app → **Authentication** → **Single-page application** platform.
2. Add `http://localhost:3000` and production URI `https://<your-frontend-fqdn>/`.
3. Enable **ID tokens** and **access tokens** as needed for your flows.

### 5. Grant SPA permission to API

1. SPA app → **API permissions** → **Add permission** → **My APIs** → select **Whitelabel API**.
2. Delegated → select `access_as_user` → **Grant admin consent** (if applicable).

### 6. Token configuration

1. Ensure **ID tokens** and **access tokens** can include required claims (optional claims: email, preferred_username, etc., in token configuration).

### 7. Configure code

- **Frontend `.env`:** `VITE_MSAL_CLIENT_ID` (SPA), `VITE_MSAL_AUTHORITY` (`https://login.microsoftonline.com/<platform-tenant-id>`), `VITE_API_SCOPE` (`api://.../access_as_user`), `VITE_API_BASE_URL`.
- **Backend `appsettings` / env:** `AzureAd:TenantId`, `AzureAd:ClientId` (API), `AzureAd:Audience` (often `api://<api-client-id>`), `AzureAd:Instance`, optional `AzureAd:Domain`.

### 8. GitHub OIDC to Azure

1. Create an **App registration** (or use existing) for the pipeline → **Certificates & secrets** → **Federated credentials** → **Add** → GitHub Actions issuer → your org/repo/ref.
2. Create a **service principal** for the app; assign **Contributor** (or narrower custom role) on the subscription or resource group.
3. Store `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` as GitHub secrets.

### 9. GitHub secrets

Add secrets listed in [CI/CD](#cicd-pipeline-github-actions); align `VITE_*` URLs with deployed Container App FQDNs.

### 10. Invite B2B guest users

1. Entra → **Users** → **New user** → **Invite external user** → send invitation to a customer mailbox.
2. Guest accepts → appears in platform tenant as **Guest**.

### 11. Test login from external tenants

1. Use a user from another Entra tenant (after invitation or self-service flows you configure).
2. Sign in via **Whitelabel SPA**; acquire token for API scope; call **`GET /secure-data`** and verify `tid` / `preferred_username` and tenant mapping logic.

---

## Final setup checklist

- [ ] Platform Entra tenant chosen; **API** and **SPA** registrations created.
- [ ] API **Application ID URI** and **scope** exposed; SPA has delegated permission and consent.
- [ ] Redirect URIs include local and production SPA URLs.
- [ ] `appsettings` / env: `AzureAd` matches API registration and audience.
- [ ] Frontend `.env` / Docker build args: MSAL + `VITE_API_BASE_URL` correct per environment.
- [ ] Sample tenants in `appsettings.json` updated with realistic `EntraTenantId` / domains for your tests.
- [ ] **Resource group** and **ACR name** (globally unique) chosen; `infra/params/dev.bicepparam` updated.
- [ ] GitHub **OIDC** federated credential and **secrets** configured; workflow runs on `main`.
- [ ] B2B guest invited and end-to-end login verified.
- [ ] **Admin RBAC:** Set **`Admin:ApplicationAdminObjectIds`** and per-tenant **`TenantAdminObjectIds`** to real Entra user **`oid`** values (see [Roles and access control](#roles-and-access-control-platform-admin-tenant-admin-tenant-user)).
- [ ] **Database (Azure):** **`POSTGRES_ADMIN_PASSWORD`** secret set for CI/CD; Bicep provisions **Azure Database for PostgreSQL (Flexible Server)** and wires the backend Container App. Local/docker: see [README-MANUAL.md](README-MANUAL.md).

---

## License

Starter template — adapt and license your derivative work as needed.
