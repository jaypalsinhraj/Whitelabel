import { Configuration, LogLevel, PublicClientApplication } from "@azure/msal-browser";

const clientId = (import.meta.env.VITE_MSAL_CLIENT_ID ?? "").trim();
const authority = (import.meta.env.VITE_MSAL_AUTHORITY ?? "").trim();
const redirectUri = (import.meta.env.VITE_MSAL_REDIRECT_URI ?? window.location.origin).trim() || window.location.origin;

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
    navigateToLoginRequestUrl: true,
  },
  cache: {
    cacheLocation: "sessionStorage",
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      logLevel: import.meta.env.DEV ? LogLevel.Verbose : LogLevel.Error,
    },
  },
};

/** OIDC scopes for the sign-in redirect only. API scope is requested separately via acquireToken*. */
export const loginRequest = {
  scopes: ["openid", "profile", "email"],
};

export function apiRequest() {
  const scope = import.meta.env.VITE_API_SCOPE;
  if (!scope) {
    throw new Error("VITE_API_SCOPE is not configured.");
  }
  return { scopes: [scope] };
}

export const msalInstance = new PublicClientApplication(msalConfig);
