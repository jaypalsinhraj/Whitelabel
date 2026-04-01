import type { TenantConfiguration } from "@/tenant/types";
import type { AccountInfo, SilentRequest } from "@azure/msal-browser";
import { InteractionRequiredAuthError } from "@azure/msal-browser";

function needsInteractiveToken(e: unknown): boolean {
  if (e instanceof InteractionRequiredAuthError) {
    return true;
  }
  if (typeof e === "object" && e !== null && "errorCode" in e) {
    const code = (e as { errorCode?: string }).errorCode;
    return (
      code === "interaction_required" ||
      code === "consent_required" ||
      code === "login_required"
    );
  }
  return false;
}
import { apiRequest, msalInstance } from "@/auth/msalConfig";

/** Thrown when MSAL starts a redirect to obtain API consent — not a user-facing error. */
export class ApiTokenRedirectPending extends Error {
  constructor() {
    super("API_TOKEN_REDIRECT");
    this.name = "ApiTokenRedirectPending";
  }
}

function baseUrl(): string {
  const url = import.meta.env.VITE_API_BASE_URL;
  if (!url) {
    throw new Error("VITE_API_BASE_URL is not configured.");
  }
  return url.replace(/\/$/, "");
}

async function getAccessToken(account: AccountInfo | null): Promise<string> {
  if (!account) {
    throw new Error("No signed-in account.");
  }
  const silent: SilentRequest = {
    ...apiRequest(),
    account,
  };
  try {
    const result = await msalInstance.acquireTokenSilent(silent);
    return result.accessToken;
  } catch (e) {
    if (needsInteractiveToken(e)) {
      await msalInstance.acquireTokenRedirect({
        ...apiRequest(),
        account,
      });
      throw new ApiTokenRedirectPending();
    }
    const code = e && typeof e === "object" && "errorCode" in e ? String((e as { errorCode?: string }).errorCode) : "";
    const msg = e instanceof Error ? e.message : String(e);
    throw new Error(
      `Silent token acquisition failed${code ? ` (${code})` : ""}: ${msg}. ` +
        `Confirm VITE_API_SCOPE matches the API exposed scope (e.g. api://<api-app-id>/access_as_user), ` +
        `the SPA has delegated permission to that API, and admin consent is granted.`,
    );
  }
}

function tenantHeaders(): Record<string, string> {
  const id = import.meta.env.VITE_TENANT_ID;
  if (id) {
    return { "X-Tenant-Id": id };
  }
  return {};
}

export async function fetchTenantConfiguration(): Promise<TenantConfiguration> {
  const res = await fetch(`${baseUrl()}/tenant`, {
    credentials: "omit",
    headers: tenantHeaders(),
  });
  if (!res.ok) {
    throw new Error(`Tenant API error: ${res.status}`);
  }
  return res.json() as Promise<TenantConfiguration>;
}

export async function fetchSecureData(account: AccountInfo | null): Promise<unknown> {
  const token = await getAccessToken(account);
  const res = await fetch(`${baseUrl()}/secure-data`, {
    headers: {
      Authorization: `Bearer ${token}`,
      ...tenantHeaders(),
    },
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `API error: ${res.status}`);
  }
  return res.json();
}

async function authorizedFetch(account: AccountInfo | null, path: string, init?: RequestInit): Promise<Response> {
  const token = await getAccessToken(account);
  return fetch(`${baseUrl()}${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      ...tenantHeaders(),
      ...(init?.headers ?? {}),
    },
  });
}

export interface SecureDataResponse {
  message: string;
  resolvedBrandingTenantId: string | null;
  userMappedTenantId: string | null;
  roles: {
    isApplicationAdmin: boolean;
    isTenantAdmin: boolean;
  };
  claims: {
    tid?: string;
    oid?: string;
    email?: string;
    preferred_username?: string;
  };
}

export interface CreateTenantPayload {
  tenantId: string;
  tenantName: string;
  primaryColor: string;
  secondaryColor: string;
  logoUrl: string;
  domain: string;
  entraTenantId: string;
  hostNames: string[];
  emailDomains: string[];
  tenantAdminObjectIds: string[];
}

export interface UpdateBrandingPayload {
  tenantName?: string;
  primaryColor?: string;
  secondaryColor?: string;
  logoUrl?: string;
  domain?: string;
  entraTenantId?: string;
}

export async function fetchSecureDataTyped(account: AccountInfo | null): Promise<SecureDataResponse> {
  return (await fetchSecureData(account)) as SecureDataResponse;
}

export async function createTenant(account: AccountInfo | null, payload: CreateTenantPayload): Promise<unknown> {
  const res = await authorizedFetch(account, "/admin/tenants", {
    method: "POST",
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    throw new Error(await res.text() || `API error: ${res.status}`);
  }
  return res.json();
}

export async function updateTenantBranding(
  account: AccountInfo | null,
  tenantId: string,
  payload: UpdateBrandingPayload,
): Promise<unknown> {
  const res = await authorizedFetch(account, `/admin/tenants/${encodeURIComponent(tenantId)}/branding`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    throw new Error(await res.text() || `API error: ${res.status}`);
  }
  return res.json();
}

export async function grantTenantUserAccess(
  account: AccountInfo | null,
  tenantId: string,
  userObjectId: string,
): Promise<unknown> {
  const res = await authorizedFetch(account, `/admin/tenants/${encodeURIComponent(tenantId)}/users`, {
    method: "POST",
    body: JSON.stringify({ userObjectId }),
  });
  if (!res.ok) {
    throw new Error(await res.text() || `API error: ${res.status}`);
  }
  return res.json();
}

export async function listTenantUsers(account: AccountInfo | null, tenantId: string): Promise<string[]> {
  const res = await authorizedFetch(account, `/admin/tenants/${encodeURIComponent(tenantId)}/users`);
  if (!res.ok) {
    throw new Error(await res.text() || `API error: ${res.status}`);
  }
  const data = (await res.json()) as { users: string[] };
  return data.users;
}
