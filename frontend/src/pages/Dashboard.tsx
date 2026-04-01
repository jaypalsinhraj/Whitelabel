import { useAccount, useMsal } from "@azure/msal-react";
import { Link } from "react-router-dom";
import { useCallback, useEffect, useState } from "react";
import { ApiTokenRedirectPending, fetchSecureDataTyped, type SecureDataResponse } from "@/services/apiClient";
import { useTenant } from "@/tenant/TenantContext";

export function DashboardPage() {
  const { instance, accounts } = useMsal();
  const account = useAccount();
  const { tenant } = useTenant();
  const [data, setData] = useState<SecureDataResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const primary = tenant?.primaryColor ?? "#1a56db";

  const load = useCallback(async () => {
    if (!account) {
      return;
    }
    setError(null);
    try {
      const json = await fetchSecureDataTyped(account);
      setData(json);
    } catch (e) {
      if (e instanceof ApiTokenRedirectPending) {
        setError(null);
        setData(null);
        return;
      }
      setError(e instanceof Error ? e.message : "Failed to load secure data");
    }
  }, [account]);

  useEffect(() => {
    if (!account) {
      return;
    }
    void load();
  }, [account, load]);

  // Session exists but MSAL "active" account not set yet (common after redirect).
  if (!account && accounts.length > 0) {
    return (
      <div style={{ padding: "2rem", fontFamily: "system-ui, sans-serif", maxWidth: 560, margin: "4rem auto" }}>
        <p style={{ margin: 0 }}>Preparing your session…</p>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 800, margin: "0 auto", padding: "2rem" }}>
      <header
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: "1rem",
          marginBottom: "2rem",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
          {tenant?.logoUrl ? (
            <img src={tenant.logoUrl} alt="" style={{ maxHeight: 40 }} />
          ) : null}
          <h1 style={{ margin: 0, fontSize: "1.5rem" }}>{tenant?.tenantName ?? "Dashboard"}</h1>
        </div>
        <button
          type="button"
          onClick={() => instance.logoutRedirect()}
          style={{
            padding: "0.5rem 1rem",
            borderRadius: 8,
            border: `1px solid ${primary}`,
            background: "#fff",
            color: primary,
            cursor: "pointer",
          }}
        >
          Sign out
        </button>
      </header>

      {(data?.roles.isApplicationAdmin || data?.roles.isTenantAdmin) ? (
        <p style={{ marginTop: "-0.5rem" }}>
          <Link to="/admin" style={{ color: primary }}>
            Open Admin Console
          </Link>
        </p>
      ) : null}

      <section
        style={{
          background: "#fff",
          borderRadius: 12,
          padding: "1.5rem",
          boxShadow: "0 4px 24px rgba(0,0,0,0.06)",
        }}
      >
        <h2 style={{ marginTop: 0 }}>Signed-in user</h2>
        <dl style={{ display: "grid", gridTemplateColumns: "140px 1fr", gap: "0.5rem 1rem" }}>
          <dt style={{ color: "#6b7280" }}>Name</dt>
          <dd style={{ margin: 0 }}>{account?.name ?? "—"}</dd>
          <dt style={{ color: "#6b7280" }}>Username</dt>
          <dd style={{ margin: 0 }}>{account?.username ?? "—"}</dd>
          <dt style={{ color: "#6b7280" }}>Tenant ID</dt>
          <dd style={{ margin: 0 }}>{account?.tenantId ?? "—"}</dd>
        </dl>
      </section>

      <section
        style={{
          marginTop: "1.5rem",
          background: "#fff",
          borderRadius: 12,
          padding: "1.5rem",
          boxShadow: "0 4px 24px rgba(0,0,0,0.06)",
        }}
      >
        <h2 style={{ marginTop: 0 }}>Secure API response</h2>
        {error ? (
          <p style={{ color: "#b91c1c" }}>{error}</p>
        ) : (
          <pre
            style={{
              overflow: "auto",
              fontSize: 13,
              background: "#f9fafb",
              padding: "1rem",
              borderRadius: 8,
            }}
          >
            {data ? JSON.stringify(data, null, 2) : "Loading…"}
          </pre>
        )}
        <button
          type="button"
          onClick={() => void load()}
          style={{
            marginTop: "1rem",
            padding: "0.5rem 1rem",
            borderRadius: 8,
            border: "none",
            background: primary,
            color: "#fff",
            cursor: "pointer",
          }}
        >
          Refresh
        </button>
      </section>

      <p style={{ marginTop: "2rem" }}>
        <Link to="/login" style={{ color: primary }}>
          Back to login
        </Link>
      </p>
    </div>
  );
}
