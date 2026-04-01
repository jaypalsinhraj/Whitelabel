import type { IPublicClientApplication } from "@azure/msal-browser";
import { InteractionStatus } from "@azure/msal-browser";
import { useMsal } from "@azure/msal-react";
import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { loginRequest } from "@/auth/msalConfig";
import { useTenant } from "@/tenant/TenantContext";

function msalConfigError(instance: IPublicClientApplication): string | null {
  const auth = instance.getConfiguration().auth;
  const id = auth.clientId?.trim() ?? "";
  const authority = auth.authority?.trim() ?? "";
  if (!id) {
    return "MSAL has no client ID. Set VITE_MSAL_CLIENT_ID in frontend/.env. If you use Docker: rebuild with docker compose build --no-cache frontend (or ensure frontend/.env is in the build context). If you use npm run dev: restart the dev server. Then hard-refresh the browser (Ctrl+Shift+R) to avoid a cached old bundle.";
  }
  if (!authority) {
    return "MSAL has no authority. Set VITE_MSAL_AUTHORITY in frontend/.env and restart the dev server.";
  }
  return null;
}

export function LoginPage() {
  const { instance, accounts, inProgress } = useMsal();
  const navigate = useNavigate();
  const location = useLocation();
  const from =
    (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? "/dashboard";
  const { tenant, loading, error: tenantError } = useTenant();
  const [loginError, setLoginError] = useState<string | null>(null);

  const configError = msalConfigError(instance);

  useEffect(() => {
    if (accounts.length > 0) {
      navigate(from, { replace: true });
    }
  }, [accounts.length, from, navigate]);

  const primary = tenant?.primaryColor ?? "#1a56db";

  async function handleSignIn() {
    setLoginError(null);
    const cfgErr = msalConfigError(instance);
    if (cfgErr) {
      setLoginError(cfgErr);
      return;
    }
    if (inProgress !== InteractionStatus.None) {
      setLoginError("Another sign-in is already in progress. Wait a moment and try again.");
      return;
    }
    try {
      await instance.loginRedirect(loginRequest);
    } catch (e) {
      const message = e instanceof Error ? e.message : String(e);
      setLoginError(message || "Sign-in failed. Check the browser console and Entra app registration.");
    }
  }

  return (
    <div
      style={{
        maxWidth: 420,
        margin: "4rem auto",
        padding: "2rem",
        borderRadius: 12,
        background: "#fff",
        boxShadow: "0 10px 40px rgba(0,0,0,0.08)",
      }}
    >
      {tenant?.logoUrl ? (
        <img
          src={tenant.logoUrl}
          alt={tenant.tenantName}
          style={{ maxHeight: 48, marginBottom: "1rem" }}
        />
      ) : null}
      <h1 style={{ marginTop: 0, color: "#111827" }}>
        {loading ? "Sign in" : tenant?.tenantName ?? "Sign in"}
      </h1>
      <p style={{ color: "#4b5563" }}>
        Use your organization account. External tenants authenticate as B2B guests in the platform
        directory.
      </p>
      {tenantError ? (
        <p style={{ color: "#b45309", fontSize: 14, marginBottom: 0 }}>
          Branding could not be loaded ({tenantError}). You can still sign in.
        </p>
      ) : null}
      {loginError || configError ? (
        <p style={{ color: "#b91c1c", fontSize: 14, marginBottom: 0, whiteSpace: "pre-wrap" }}>
          {loginError ?? configError}
        </p>
      ) : null}
      <button
        type="button"
        onClick={() => void handleSignIn()}
        disabled={inProgress !== InteractionStatus.None}
        style={{
          marginTop: "1rem",
          padding: "0.75rem 1.25rem",
          borderRadius: 8,
          border: "none",
          background: primary,
          color: "#fff",
          fontWeight: 600,
          cursor: inProgress !== InteractionStatus.None ? "wait" : "pointer",
          width: "100%",
          opacity: inProgress !== InteractionStatus.None ? 0.85 : 1,
        }}
      >
        {inProgress !== InteractionStatus.None ? "Opening Microsoft sign-in…" : "Sign in with Microsoft"}
      </button>
      <p style={{ marginTop: "1.5rem", fontSize: 14 }}>
        <Link to="/dashboard" style={{ color: primary }}>
          Continue to dashboard (requires sign-in)
        </Link>
      </p>
    </div>
  );
}
