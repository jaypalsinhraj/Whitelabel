import { useTenant } from "@/tenant/TenantContext";

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { tenant } = useTenant();

  const primary = tenant?.primaryColor ?? "#1a56db";
  const secondary = tenant?.secondaryColor ?? "#0f3b99";

  // Do not block the whole app on tenant fetch — sign-in and MSAL must work even if the API is down.
  return (
    <div
      style={
        {
          minHeight: "100vh",
          fontFamily: "system-ui, Segoe UI, sans-serif",
          background: `linear-gradient(160deg, ${primary}12, ${secondary}08)`,
          color: "#111827",
          "--tenant-primary": primary,
          "--tenant-secondary": secondary,
        } as React.CSSProperties
      }
    >
      {children}
    </div>
  );
}
