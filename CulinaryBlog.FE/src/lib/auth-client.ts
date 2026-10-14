export interface AuthResponse {
  id: string;
  username: string;
  fullName: string;
  email: string;
  accessToken: string;
  refreshToken?: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
}

export function saveAuthResponse(response: AuthResponse, storeRefreshToken = true): void {
  if (typeof window === "undefined") return;

  window.localStorage.setItem("accessToken", response.accessToken);
  if (storeRefreshToken && response.refreshToken) {
    window.localStorage.setItem("refreshToken", response.refreshToken);
  } else {
    window.localStorage.removeItem("refreshToken");
  }
  window.localStorage.setItem("accessTokenExpiresAt", response.accessTokenExpiresAt);
}

export function clearAuth(): void {
  if (typeof window === "undefined") return;

  window.localStorage.removeItem("accessToken");
  window.localStorage.removeItem("refreshToken");
  window.localStorage.removeItem("accessTokenExpiresAt");
}
