const apiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");

export interface AuthResponse {
  id: string;
  userName: string;
  fullName: string;
  email: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
}

interface AuthStorage {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

let refreshPromise: Promise<void> | null = null;

function getStoredAuth(): AuthStorage | null {
  if (typeof window === "undefined") return null;

  const accessToken = window.localStorage.getItem("accessToken");
  const refreshToken = window.localStorage.getItem("refreshToken");
  const accessTokenExpiresAt = window.localStorage.getItem("accessTokenExpiresAt");

  if (!accessToken || !refreshToken || !accessTokenExpiresAt) return null;
  return { accessToken, refreshToken, accessTokenExpiresAt };
}

export function saveAuthResponse(response: AuthResponse): void {
  if (typeof window === "undefined") return;

  window.localStorage.setItem("accessToken", response.accessToken);
  window.localStorage.setItem("refreshToken", response.refreshToken);
  window.localStorage.setItem("accessTokenExpiresAt", response.accessTokenExpiresAt);
}

export function clearAuth(): void {
  if (typeof window === "undefined") return;

  window.localStorage.removeItem("accessToken");
  window.localStorage.removeItem("refreshToken");
  window.localStorage.removeItem("accessTokenExpiresAt");
}

async function refreshAuth(): Promise<void> {
  const auth = getStoredAuth();
  if (!auth) {
    clearAuth();
    throw new Error("Token đăng nhập đã hết hạn.");
  }

  const response = await fetch(`${apiUrl}/v1/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: auth.refreshToken }),
  });

  if (!response.ok) {
    clearAuth();
    throw new Error("Không thể làm mới token đăng nhập.");
  }

  saveAuthResponse(await response.json() as AuthResponse);
}

export async function authenticatedFetch<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const auth = getStoredAuth();
  const response = await fetch(`${apiUrl}${path}`, {
    ...init,
    headers: {
      ...init.headers,
      ...(auth ? { Authorization: `Bearer ${auth.accessToken}` } : {}),
    },
  });

  if (response.status !== 401) {
    return response.json() as Promise<T>;
  }

  refreshPromise ??= refreshAuth();
  try {
    await refreshPromise;
    const retryAuth = getStoredAuth();
    const retryResponse = await fetch(`${apiUrl}${path}`, {
      ...init,
      headers: {
        ...init.headers,
        ...(retryAuth ? { Authorization: `Bearer ${retryAuth.accessToken}` } : {}),
      },
    });

    if (!retryResponse.ok) {
      throw new Error(`Yêu cầu thất bại (${retryResponse.status}).`);
    }
    return retryResponse.json() as Promise<T>;
  } finally {
    refreshPromise = null;
  }
}
