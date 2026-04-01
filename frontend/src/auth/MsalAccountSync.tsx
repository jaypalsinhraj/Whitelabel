import { useMsal } from "@azure/msal-react";
import { useEffect } from "react";

/**
 * useAccount() / acquireTokenSilent use the "active" account. MSAL does not always set it
 * after redirect or when restoring from cache — set it when there is exactly one session.
 */
export function MsalAccountSync() {
  const { instance } = useMsal();

  useEffect(() => {
    if (instance.getActiveAccount()) {
      return;
    }
    const accounts = instance.getAllAccounts();
    if (accounts.length === 1) {
      instance.setActiveAccount(accounts[0]);
    } else if (accounts.length > 1) {
      instance.setActiveAccount(accounts.find((a) => a.tenantId) ?? accounts[0]);
    }
  }, [instance]);

  return null;
}
