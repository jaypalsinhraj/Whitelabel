import { useAccount } from "@azure/msal-react";
import { useEffect, useMemo, useState } from "react";
import type { CSSProperties, FormEvent } from "react";
import { Link } from "react-router-dom";
import {
  ApiTokenRedirectPending,
  createTenant,
  fetchSecureDataTyped,
  grantTenantUserAccess,
  updateTenantBranding,
  type SecureDataResponse,
} from "@/services/apiClient";
import { useTenant } from "@/tenant/TenantContext";

export function AdminPage() {
  const account = useAccount();
  const { tenant } = useTenant();
  const [roles, setRoles] = useState<SecureDataResponse["roles"] | null>(null);
  const [rolesLoading, setRolesLoading] = useState(true);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [newTenant, setNewTenant] = useState({
    tenantId: "",
    tenantName: "",
    primaryColor: "#1a56db",
    secondaryColor: "#0f3b99",
    logoUrl: "",
    domain: "",
    entraTenantId: "",
    hostNames: "",
    emailDomains: "",
    tenantAdminObjectIds: "",
  });

  const [branding, setBranding] = useState({
    tenantName: tenant?.tenantName ?? "",
    primaryColor: tenant?.primaryColor ?? "#1a56db",
    secondaryColor: tenant?.secondaryColor ?? "#0f3b99",
    logoUrl: tenant?.logoUrl ?? "",
    domain: tenant?.domain ?? "",
    entraTenantId: tenant?.entraTenantId ?? "",
  });

  const [userObjectId, setUserObjectId] = useState("");
  const currentTenantId = tenant?.tenantId ?? "";

  const disabled = useMemo(() => !account, [account]);

  const isApplicationAdmin = roles?.isApplicationAdmin === true;
  const isTenantAdmin = roles?.isTenantAdmin === true;
  const showPlatformPanels = isApplicationAdmin;
  const showTenantBrandingPanel = isApplicationAdmin || isTenantAdmin;

  useEffect(() => {
    if (!account) {
      setRolesLoading(false);
      return;
    }
    let cancelled = false;
    setRolesLoading(true);
    void (async () => {
      try {
        const data = await fetchSecureDataTyped(account);
        if (!cancelled) {
          setRoles(data.roles);
        }
      } catch (e) {
        if (e instanceof ApiTokenRedirectPending) {
          return;
        }
        if (!cancelled) {
          setError(e instanceof Error ? e.message : "Could not load permissions.");
        }
      } finally {
        if (!cancelled) {
          setRolesLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [account]);

  useEffect(() => {
    setBranding((b) => ({
      ...b,
      tenantName: tenant?.tenantName ?? b.tenantName,
      primaryColor: tenant?.primaryColor ?? b.primaryColor,
      secondaryColor: tenant?.secondaryColor ?? b.secondaryColor,
      logoUrl: tenant?.logoUrl ?? b.logoUrl,
      domain: tenant?.domain ?? b.domain,
      entraTenantId: tenant?.entraTenantId ?? b.entraTenantId,
    }));
  }, [tenant]);

  const clear = () => {
    setError(null);
    setStatus(null);
  };

  async function onCreateTenant(e: FormEvent) {
    e.preventDefault();
    clear();
    try {
      await createTenant(account, {
        tenantId: newTenant.tenantId.trim(),
        tenantName: newTenant.tenantName.trim(),
        primaryColor: newTenant.primaryColor.trim(),
        secondaryColor: newTenant.secondaryColor.trim(),
        logoUrl: newTenant.logoUrl.trim(),
        domain: newTenant.domain.trim(),
        entraTenantId: newTenant.entraTenantId.trim(),
        hostNames: splitCsv(newTenant.hostNames),
        emailDomains: splitCsv(newTenant.emailDomains),
        tenantAdminObjectIds: splitCsv(newTenant.tenantAdminObjectIds),
      });
      setStatus("Tenant created.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create tenant.");
    }
  }

  async function onUpdateBranding(e: FormEvent) {
    e.preventDefault();
    clear();
    if (!currentTenantId) {
      setError("No tenant resolved.");
      return;
    }
    try {
      await updateTenantBranding(account, currentTenantId, branding);
      setStatus("Branding updated.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update branding.");
    }
  }

  async function onGrantAccess(e: FormEvent) {
    e.preventDefault();
    clear();
    if (!currentTenantId) {
      setError("No tenant resolved.");
      return;
    }
    try {
      await grantTenantUserAccess(account, currentTenantId, userObjectId.trim());
      setStatus("User access granted.");
      setUserObjectId("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to grant access.");
    }
  }

  if (!account) {
    return (
      <div style={{ padding: "2rem", fontFamily: "system-ui, sans-serif" }}>
        <p>Sign in to use the admin console.</p>
        <Link to="/login">Go to login</Link>
      </div>
    );
  }

  if (rolesLoading) {
    return (
      <div style={{ padding: "2rem", fontFamily: "system-ui, sans-serif", maxWidth: 560, margin: "4rem auto" }}>
        <p style={{ margin: 0 }}>Loading permissions…</p>
      </div>
    );
  }

  if (!showTenantBrandingPanel) {
    return (
      <div style={{ maxWidth: 560, margin: "4rem auto", padding: "2rem", fontFamily: "system-ui, sans-serif" }}>
        <h1 style={{ marginTop: 0 }}>Admin</h1>
        <p style={{ color: "#4b5563" }}>You do not have tenant or application admin access.</p>
        <Link to="/dashboard">Back to dashboard</Link>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 900, margin: "0 auto", padding: "2rem" }}>
      <h1>Admin Console</h1>
      <p style={{ color: "#4b5563" }}>
        {isApplicationAdmin
          ? "Application admins can create tenants, update branding, and grant user access."
          : "Tenant admins can update white-label settings for the current tenant only."}
      </p>
      {status ? <p style={{ color: "#065f46" }}>{status}</p> : null}
      {error ? <p style={{ color: "#b91c1c", whiteSpace: "pre-wrap" }}>{error}</p> : null}

      {showPlatformPanels ? (
      <section style={cardStyle}>
        <h2>Create tenant (Application Admin)</h2>
        <form onSubmit={(e) => void onCreateTenant(e)} style={formStyle}>
          <input value={newTenant.tenantId} onChange={(e) => setNewTenant({ ...newTenant, tenantId: e.target.value })} placeholder="tenantId" required />
          <input value={newTenant.tenantName} onChange={(e) => setNewTenant({ ...newTenant, tenantName: e.target.value })} placeholder="tenantName" required />
          <input value={newTenant.primaryColor} onChange={(e) => setNewTenant({ ...newTenant, primaryColor: e.target.value })} placeholder="primaryColor (#hex)" />
          <input value={newTenant.secondaryColor} onChange={(e) => setNewTenant({ ...newTenant, secondaryColor: e.target.value })} placeholder="secondaryColor (#hex)" />
          <input value={newTenant.logoUrl} onChange={(e) => setNewTenant({ ...newTenant, logoUrl: e.target.value })} placeholder="logoUrl" />
          <input value={newTenant.domain} onChange={(e) => setNewTenant({ ...newTenant, domain: e.target.value })} placeholder="domain" />
          <input value={newTenant.entraTenantId} onChange={(e) => setNewTenant({ ...newTenant, entraTenantId: e.target.value })} placeholder="EntraTenantId" />
          <input value={newTenant.hostNames} onChange={(e) => setNewTenant({ ...newTenant, hostNames: e.target.value })} placeholder="host names CSV" />
          <input value={newTenant.emailDomains} onChange={(e) => setNewTenant({ ...newTenant, emailDomains: e.target.value })} placeholder="email domains CSV" />
          <input value={newTenant.tenantAdminObjectIds} onChange={(e) => setNewTenant({ ...newTenant, tenantAdminObjectIds: e.target.value })} placeholder="tenant admin object IDs CSV" />
          <button type="submit" disabled={disabled}>Create tenant</button>
        </form>
      </section>
      ) : null}

      <section style={cardStyle}>
        <h2>Update current tenant branding</h2>
        <p style={{ marginTop: 0, color: "#6b7280" }}>Current tenant: {currentTenantId || "—"}</p>
        <form onSubmit={(e) => void onUpdateBranding(e)} style={formStyle}>
          <input value={branding.tenantName} onChange={(e) => setBranding({ ...branding, tenantName: e.target.value })} placeholder="tenantName" />
          <input value={branding.primaryColor} onChange={(e) => setBranding({ ...branding, primaryColor: e.target.value })} placeholder="primaryColor (#hex)" />
          <input value={branding.secondaryColor} onChange={(e) => setBranding({ ...branding, secondaryColor: e.target.value })} placeholder="secondaryColor (#hex)" />
          <input value={branding.logoUrl} onChange={(e) => setBranding({ ...branding, logoUrl: e.target.value })} placeholder="logoUrl" />
          <input value={branding.domain} onChange={(e) => setBranding({ ...branding, domain: e.target.value })} placeholder="domain" />
          <input value={branding.entraTenantId} onChange={(e) => setBranding({ ...branding, entraTenantId: e.target.value })} placeholder="EntraTenantId" />
          <button type="submit" disabled={disabled || !currentTenantId}>Update branding</button>
        </form>
      </section>

      {showPlatformPanels ? (
      <section style={cardStyle}>
        <h2>Grant tenant access</h2>
        <form onSubmit={(e) => void onGrantAccess(e)} style={formStyle}>
          <input value={userObjectId} onChange={(e) => setUserObjectId(e.target.value)} placeholder="User Object ID (oid)" required />
          <button type="submit" disabled={disabled || !currentTenantId}>Grant access</button>
        </form>
      </section>
      ) : null}

      <p style={{ marginTop: "1.5rem" }}>
        <Link to="/dashboard">Back to dashboard</Link>
      </p>
    </div>
  );
}

function splitCsv(value: string): string[] {
  return value.split(",").map((x) => x.trim()).filter(Boolean);
}

const cardStyle: CSSProperties = {
  background: "#fff",
  borderRadius: 12,
  padding: "1.5rem",
  boxShadow: "0 4px 24px rgba(0,0,0,0.06)",
  marginTop: "1rem",
};

const formStyle: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "1fr",
  gap: "0.6rem",
};
