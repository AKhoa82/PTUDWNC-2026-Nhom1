import { getToken } from "next-auth/jwt";
import type { AuthResponse } from "@/lib/auth-client";

const apiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");

export async function POST(request: Request) {
  const origin = request.headers.get("origin");
  if (origin && origin !== new URL(request.url).origin) return new Response(null, { status: 403 });

  const secret = process.env.AUTH_SECRET;
  if (!secret) return new Response(null, { status: 500 });

  const token = await getToken({ req: request, secret, secureCookie: process.env.NODE_ENV === "production" });
  const backendAuth = token?.backendAuth as AuthResponse | undefined;
  if (!backendAuth?.refreshToken) return new Response(null, { status: 401 });

  const production = process.env.NODE_ENV === "production";
  try {
    const response = await fetch(`${apiUrl}/v1/auth/logout`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(backendAuth.accessToken ? { Authorization: `Bearer ${backendAuth.accessToken}` } : {}),
        ...(production ? { Cookie: `culinaryblog_refresh=${encodeURIComponent(backendAuth.refreshToken)}` } : {}),
      },
      body: JSON.stringify({ refreshToken: production ? "" : backendAuth.refreshToken }),
      cache: "no-store",
    });
    return new Response(null, { status: response.status === 204 ? 204 : 502 });
  } catch {
    return new Response(null, { status: 502 });
  }
}
