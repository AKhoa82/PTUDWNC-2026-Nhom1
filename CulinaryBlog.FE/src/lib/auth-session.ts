export const AUTH_CHANGED_EVENT = "culinaryblog:auth-changed";

export type UserRole = "admin" | "author" | "reader";

export type AuthSession = { id: string; name: string; role: UserRole };

export type LoginResponse = {
  username: string;
  fullName: string;
  accessToken: string;
  refreshToken: string;
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

export function saveAuthSession(response: LoginResponse): void {
  localStorage.setItem("accessToken", response.accessToken);
  localStorage.setItem("refreshToken", response.refreshToken);
  localStorage.setItem("accessTokenExpiresAt", response.accessTokenExpiresAt);
  localStorage.setItem("userName", response.fullName || response.username);
  window.dispatchEvent(new Event(AUTH_CHANGED_EVENT));
}
