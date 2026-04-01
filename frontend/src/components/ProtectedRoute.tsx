import { InteractionStatus } from "@azure/msal-browser";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { Navigate, useLocation } from "react-router-dom";

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const location = useLocation();

  if (inProgress === InteractionStatus.Login || inProgress === InteractionStatus.SsoSilent) {
    return (
      <div style={{ padding: "2rem", fontFamily: "system-ui" }}>
        Checking sign-in status…
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <>{children}</>;
}
