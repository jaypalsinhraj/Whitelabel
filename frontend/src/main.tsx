import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { msalInstance } from "@/auth/msalConfig";
import App from "./App";

function ensureActiveAccount() {
  if (msalInstance.getActiveAccount()) {
    return;
  }
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 1) {
    msalInstance.setActiveAccount(accounts[0]);
  } else if (accounts.length > 1) {
    const home = accounts.find((a) => a.tenantId) ?? accounts[0];
    msalInstance.setActiveAccount(home);
  }
}

async function bootstrap() {
  await msalInstance.initialize();
  const redirect = await msalInstance.handleRedirectPromise();
  if (redirect?.account) {
    msalInstance.setActiveAccount(redirect.account);
  } else {
    ensureActiveAccount();
  }
  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}

void bootstrap();
