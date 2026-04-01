import { MsalProvider } from "@azure/msal-react";
import { msalInstance } from "./msalConfig";
import { MsalAccountSync } from "./MsalAccountSync";

export function AuthProvider({ children }: { children: React.ReactNode }) {
  return (
    <MsalProvider instance={msalInstance}>
      <MsalAccountSync />
      {children}
    </MsalProvider>
  );
}
