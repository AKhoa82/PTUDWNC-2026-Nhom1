import { getSession, signOut } from "next-auth/react";
import { clearAuth, type AuthResponse } from "./auth-client";

export const AUTH_CHANGED_EVENT = "culinaryblog:auth-changed";
const apiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");
let refreshPromise: Promise<boolean> | null = null;
let logoutInProgress = false;

export type UserRole = "admin" | "author" | "reader";

export type AuthSession = { id: string; name: string; role: UserRole };

export type LoginResponse = {
  username: string;
  fullName: string;
  accessToken: string;
  refreshToken?: string;
  accessTokenExpiresAt: string;
};

function tokenClaims(token: string): Record<string, unknown> | null {
  try {
    const encoded = token.split(".")[1];
    if (!encoded) return null;
    const base64 = encoded.replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(atob(base64)) as Record<string, unknown>;
  } catch {
    return null;
  }
}

export function readAuthSession(): AuthSession | null {
  const token = localStorage.getItem("accessToken");
  if (!token) return null;

  const claims = tokenClaims(token);
  if (!claims) return null;

  const storedExpiry = localStorage.getItem("accessTokenExpiresAt");
  const expiresAt = storedExpiry ? Date.parse(storedExpiry) : Number(claims.exp) * 1000;
  if (!Number.isFinite(expiresAt) || expiresAt <= Date.now()) return null;

  const rawRoles = claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? claims.role;
  const roles = Array.isArray(rawRoles) ? rawRoles : [rawRoles];
  const role: UserRole = roles.some(value => String(value).toLowerCase() === "admin")
    ? "admin"
    : roles.some(value => String(value).toLowerCase() === "author")
      ? "author"
      : "reader";

  const name = localStorage.getItem("userName") ||
    String(claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] ?? "Tài khoản");
  const id = String(claims.sub ?? claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] ?? "");
  if (!id) return null;
  return { id, name, role };
}

export function saveAuthSession(response: LoginResponse, provider: "email" | "google" = "email"): void {
  localStorage.setItem("accessToken", response.accessToken);
  if (response.refreshToken) localStorage.setItem("refreshToken", response.refreshToken);
  else localStorage.removeItem("refreshToken");
  localStorage.setItem("accessTokenExpiresAt", response.accessTokenExpiresAt);
  localStorage.setItem("userName", response.fullName || response.username);
  localStorage.setItem("authProvider", provider);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}

export async function ensureFreshSession(force = false): Promise<boolean> {
  if (typeof window === "undefined" || logoutInProgress) return false;
  const accessToken = localStorage.getItem("accessToken");
  if (!accessToken) return false;
  const expiresAt = Date.parse(localStorage.getItem("accessTokenExpiresAt") ?? "");
  if (!force && Number.isFinite(expiresAt) && expiresAt > Date.now() + 60_000) return true;

  refreshPromise ??= refreshSession().finally(() => { refreshPromise = null; });
  return refreshPromise;
}

export async function logoutSession(): Promise<void> {
  if (typeof window === "undefined") return;
  if (refreshPromise) await refreshPromise.catch(() => false);
  logoutInProgress = true;
  try {
    if (localStorage.getItem("authProvider") === "google") {
      const revokeResponse = await fetch("/api/auth/revoke", { method: "POST", cache: "no-store" });
      if (revokeResponse.status !== 204) throw new Error("Không thể thu hồi phiên đăng nhập. Vui lòng thử lại.");
      await signOut({ redirect: false });
      clearAuth();
      return;
    }

    const accessToken = localStorage.getItem("accessToken");
    const refreshToken = localStorage.getItem("refreshToken");
    if (!accessToken && !refreshToken) {
      clearAuth();
      return;
    }

    const response = await fetch(`${apiUrl}/v1/auth/logout`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      credentials: "include",
      body: JSON.stringify({ refreshToken: refreshToken ?? "" }),
      cache: "no-store",
    });
    if (response.status !== 204) throw new Error("Không thể thu hồi phiên đăng nhập. Vui lòng thử lại.");
    clearAuth();
  } finally {
    logoutInProgress = false;
  }
}

async function refreshSession(): Promise<boolean> {
  const provider = localStorage.getItem("authProvider");
  const rawToken = localStorage.getItem("refreshToken");
  if (provider === "google" || (!provider && !rawToken)) {
    try {
      const session = await getSession();
      const auth = session?.backendAuth;
      if (auth?.accessToken && Date.parse(auth.accessTokenExpiresAt) > Date.now()) {
        saveAuthSession(auth, "google");
        return true;
      }
    } catch { /* Keep the existing session during a temporary network failure. */ }
    if (provider === "google") return false;
  }

  try {
    const response = await fetch(`${apiUrl}/v1/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify({ refreshToken: rawToken ?? "" }),
      cache: "no-store",
    });
    if (response.status === 401) {
      clearAuth();
      return false;
    }
    if (!response.ok) return false;
    const auth = await response.json() as Partial<AuthResponse>;
    if (!auth.accessToken || !auth.accessTokenExpiresAt) return false;
    saveAuthSession({
      username: auth.username ?? "",
      fullName: auth.fullName ?? "",
      accessToken: auth.accessToken,
      refreshToken: auth.refreshToken || undefined,
      accessTokenExpiresAt: auth.accessTokenExpiresAt,
    }, "email");
    return true;
  } catch {
    return false;
  }
}

export async function fetchWithAuth(input: string, options: RequestInit = {}): Promise<Response> {
  const requiresAuth = new Headers(options.headers).has("Authorization");
  if (requiresAuth) await ensureFreshSession();

  const send = () => {
    const headers = new Headers(options.headers);
    if (requiresAuth) {
      const token = localStorage.getItem("accessToken");
      if (token) headers.set("Authorization", `Bearer ${token}`);
      else headers.delete("Authorization");
    }
    return fetch(input, { ...options, headers });
  };

  const response = await send();
  if (response.status !== 401 || !requiresAuth || !await ensureFreshSession(true)) return response;
  return send();
}
