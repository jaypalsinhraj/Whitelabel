import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import type { TenantConfiguration } from "./types";
import { fetchTenantConfiguration } from "@/services/apiClient";

type TenantState = {
  tenant: TenantConfiguration | null;
  loading: boolean;
  error: string | null;
  reload: () => Promise<void>;
};

const TenantContext = createContext<TenantState | undefined>(undefined);

export function TenantProvider({ children }: { children: React.ReactNode }) {
  const [tenant, setTenant] = useState<TenantConfiguration | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const cfg = await fetchTenantConfiguration();
      setTenant(cfg);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load tenant");
      setTenant(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const value = useMemo(
    () => ({ tenant, loading, error, reload: load }),
    [tenant, loading, error, load],
  );

  return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>;
}

export function useTenant() {
  const ctx = useContext(TenantContext);
  if (!ctx) {
    throw new Error("useTenant must be used within TenantProvider");
  }
  return ctx;
}
