# Manual setup and operations

This guide covers **one-time manual steps** and **operational tasks** for the Whitelabel starter: Microsoft Entra ID (Azure AD), databases, environment variables, containers, and production hardening. The main **[README.md](README.md)** describes architecture and local development flows.

-----

## 1. Microsoft Entra ID (app registrations)

Complete these in the **Azure portal** → **Microsoft Entra ID** → **App registrations**.

### 1.1 API (backend) app

1. **New registration** → name e.g. `Whitelabel API` → account types per your policy → register.
2. Note **Application (client) ID** and **Directory (tenant) ID**.
3. **Expose an API** → set **Application ID URI** (e.g. `api://<api-client-id>`) → add a scope (e.g. `access_as_user`).
4. **App roles** (optional, for future RBAC): not required for this starter’s config-based admins.

### 1.2 SPA (frontend) app

1. **New registration** → name e.g. `Whitelabel SPA` → **single-page application** redirect URIs:
   - `http://localhost:3000`
   - your production URL, e.g. `https://<frontend-fqdn>/`
2. Note **Application (client) ID**.
3. **API permissions** → add the API app → delegated → select the exposed scope → **Grant admin consent** if your policy requires it.
4. **Authentication** → enable **ID tokens** and **access tokens** for implicit/hybrid as needed for your MSAL flow (MSAL.js typically uses auth code + PKCE).

### 1.3 Backend `appsettings.json` / environment

Map values into `AzureAd` (or equivalent environment variables):

| Setting | Source |
|--------|--------|
| `TenantId` | Directory (tenant) ID of the **platform** tenant that owns the registrations |
| `ClientId` | API app **Application (client) ID** |
| `Audience` | Often `api://<api-client-id>` or the API app client ID, matching the token `aud` |
| `Instance` | Usually `https://login.microsoftonline.com/` |

Environment variable override (double underscore):

```bash
AzureAd__TenantId=<guid>
AzureAd__ClientId=<api-client-id>
AzureAd__Audience=api://<api-client-id>
```

### 1.4 Frontend `.env`

Set (see `frontend/.env.example` if present):

- `VITE_MSAL_CLIENT_ID` = **SPA** client ID  
- `VITE_MSAL_AUTHORITY` = `https://login.microsoftonline.com/<tenant-id>` or `/common` / `/organizations` per your sign-in model  
- `VITE_MSAL_REDIRECT_URI` = SPA URL matching Entra redirect URIs  
- `VITE_API_SCOPE` = `api://<api-client-id>/<scope-name>`  
- `VITE_API_BASE_URL` = API base URL (e.g. `http://localhost:5000` or `https://<api-fqdn>`)

Rebuild the frontend after any change to `VITE_*` (they are compile-time in Vite).

---

## 2. Database (EF Core)

The API persists **tenants**, **host names**, **email domains**, **tenant admins**, **user grants**, and **application (platform) admins** in a relational database.

### 2.1 Provider

The API uses **PostgreSQL** via **Npgsql** and EF Core. `Database:Provider` is informational (`PostgreSQL`); the runtime always connects with Npgsql.

### 2.2 Connection string

Set **`ConnectionStrings:DefaultConnection`** (or env `ConnectionStrings__DefaultConnection`).

**Local / Docker Compose (default in `appsettings.json`)**

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=whitelabel;Username=postgres;Password=postgres"
},
"Database": {
  "Provider": "PostgreSQL"
}
```

In **Docker Compose**, the API uses `Host=postgres` and the same database name and credentials as the `postgres` service.

**Azure Database for PostgreSQL (example)**

```bash
ConnectionStrings__DefaultConnection="Host=<name>.postgres.database.azure.com;Database=whitelabel;Username=<user>;Password=<password>;Ssl Mode=Require;Trust Server Certificate=true"
```

Use **managed identity** or **Key Vault references** in Azure Container Apps instead of passwords in plain text when possible.

**Design-time migrations:** optional env `WHITELABEL_DESIGN_PG` overrides the connection string used by `WhitelabelDbContextFactory` (defaults to the same local connection as above).

### 2.3 Migrations

Migrations apply **automatically on API startup** (`DatabaseInitializer`).

To add a new migration after model changes (from repo `backend/`):

```bash
dotnet ef migrations add <Name> ^
  --project src/Whitelabel.Infrastructure/Whitelabel.Infrastructure.csproj ^
  --startup-project src/Whitelabel.Api/Whitelabel.Api.csproj ^
  --output-dir Persistence/Migrations
```

To apply without running the API (optional):

```bash
dotnet ef database update --project src/Whitelabel.Infrastructure/Whitelabel.Infrastructure.csproj --startup-project src/Whitelabel.Api/Whitelabel.Api.csproj
```

### 2.4 Initial seed

On **first run** with an **empty** database, the API **seeds** from `Tenants:Items` and `Admin:ApplicationAdminObjectIds` in configuration (same data shape as before persistence).

- If you **change** `Tenants:Items` after the DB already has rows, those changes **do not** overwrite existing tenants. Use the **Admin API** or direct SQL/tools to adjust data.
- **Application admin** object IDs listed under `Admin:ApplicationAdminObjectIds` are **merged** on startup: new OIDs are inserted into the `ApplicationAdmins` table if missing.

### 2.5 Platform admins in the database

Besides config, platform admins can exist in table **`ApplicationAdmins`** (`ObjectId` = user’s Entra `oid`). You can insert rows manually or rely on config sync above.

---

## 3. Tenant resolution and `X-Tenant-Id`

- **`TenantResolution:AllowTenantIdHeader`**: when `true`, the API reads **`X-Tenant-Id`** (useful for local testing).  
- **`appsettings.Production.json`** sets this to **`false`**: production should rely on **host name** / **domain** mapping unless you have a controlled gateway that sets the header safely.

---

## 4. Health checks

- **`GET /health`** — liveness (all checks).  
- **`GET /health/ready`** — includes database connectivity (`ready` tag).

Configure load balancers or Container Apps probes to use these paths.

---

## 5. Docker Compose (PostgreSQL)

The compose file defines a **`postgres:16-alpine`** service with database **`whitelabel`**, user/password **`postgres`**, and a named volume for data. The **backend** service waits for PostgreSQL to be healthy, then connects with:

`Host=postgres;Database=whitelabel;Username=postgres;Password=postgres`

To reset data, remove the **`whitelabel-pg-data`** volume (destructive).

**Do not** set empty `AzureAd__*` variables in Compose; omitted vars are safer than empty strings that override `appsettings.json` with invalid GUIDs.

---

## 6. Production checklist (concise)

1. **Entra**: production redirect URIs, API scope, consent.  
2. **Secrets**: Key Vault or Container App secrets; no real passwords or OIDs in git.  
3. **Database**: Azure Database for PostgreSQL (or another managed Postgres), backups, firewall rules.  
4. **Connection string** (Npgsql format) on the API, preferably via Key Vault or managed identity.  
5. **CORS**: `Cors:Origins` only your real SPA origins.  
6. **`TenantResolution:AllowTenantIdHeader`**: `false` unless you trust the edge.  
7. **TLS**: terminate HTTPS at the ingress; the sample API runs HTTP inside the container (use forwarded headers — enabled when not Development).  
8. **Swagger**: disabled outside Development in code; do not expose Swagger publicly in production without protection.  
9. **CI/CD**: build with placeholder `VITE_*` in pipelines; inject real frontend env at image build or runtime per your strategy.  
10. **Observability**: connect Application Insights or similar to the API and optionally the SPA.

---

## 7. Troubleshooting

| Symptom | Things to check |
|--------|------------------|
| **401** from API | `AzureAd` client ID, audience, and SPA scope; token `aud` and `iss`; Compose env overriding config with empty values. |
| **No tenants / empty UI** | PostgreSQL running and reachable; connection string; migrations ran; seed ran (empty DB only). |
| **Wrong branding tenant** | `HostNames` / `Domain` / `EmailDomains` for the tenant row; `DefaultTenantId`. |
| **Admin UI missing** | `oid` in `ApplicationAdmins` or `Admin:ApplicationAdminObjectIds`; tenant admin OIDs in `TenantAdmins` or `Tenants:Items[].TenantAdminObjectIds` at seed time. |

For OID values, call **`GET /secure-data`** when signed in and read `claims.oid`.

---

## 8. Files to edit most often

| Task | Location |
|------|----------|
| Entra / JWT | `backend/src/Whitelabel.Api/appsettings.json` or environment variables |
| CORS | `Cors:Origins` |
| Seed defaults (empty DB only) | `Tenants`, `Admin` sections in appsettings |
| Frontend API URL & MSAL | `frontend/.env` |

---

*Generated for the Whitelabel starter; adjust names and IDs to match your Azure subscription and domains.*
