import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
import { headers } from "next/headers";
import type { AuthResponse } from "@/lib/auth-client";

const googleClientId = process.env.AUTH_GOOGLE_ID ?? process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";
const apiBaseUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");

export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: process.env.NODE_ENV !== "production" || process.env.AUTH_TRUST_HOST === "true",
  session: { strategy: "jwt" },
  pages: {
    signIn: "/auth/login",
    error: "/auth/login",
  },
  providers: [
    Google({
      clientId: googleClientId,
      clientSecret: process.env.AUTH_GOOGLE_SECRET ?? "",
      checks: ["pkce", "state"],
    }),
  ],
  callbacks: {
    async jwt({ token, account }) {
      if (account?.provider === "google") {
        if (!account.id_token) throw new Error("Google did not return an ID token.");

        const requestHeaders = await headers();
        const trustedClientIpHeader = process.env.AUTH_CLIENT_IP_HEADER?.trim().toLowerCase();
        const trustedClientIp = trustedClientIpHeader
          ? requestHeaders.get(trustedClientIpHeader)
          : null;

        const response = await fetch(`${apiBaseUrl}/v1/auth/google`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            ...(trustedClientIp ? { "X-Forwarded-For": trustedClientIp } : {}),
          },
          body: JSON.stringify({ idToken: account.id_token }),
          cache: "no-store",
        });

        const result = await response.json().catch(() => null) as
          | (Partial<AuthResponse> & { message?: string })
          | null;

        const refreshToken = result?.refreshToken || readRefreshCookie(response.headers.get("set-cookie"));
        if (!response.ok || !result?.accessToken || !refreshToken) {
          throw new Error(result?.message ?? "The API could not complete Google sign-in.");
        }

        token.backendAuth = { ...result, refreshToken } as AuthResponse;
        return token;
      }

      const backendAuth = token.backendAuth as AuthResponse | undefined;
      if (!backendAuth || Date.parse(backendAuth.accessTokenExpiresAt) > Date.now() + 60_000) {
        return token;
      }

      const refreshToken = backendAuth.refreshToken;
      if (!refreshToken) {
        delete token.backendAuth;
        return token;
      }

      try {
        const production = process.env.NODE_ENV === "production";
        const response = await fetch(`${apiBaseUrl}/v1/auth/refresh`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            ...(production ? { Cookie: `culinaryblog_refresh=${encodeURIComponent(refreshToken)}` } : {}),
          },
          body: JSON.stringify({ refreshToken: production ? "" : refreshToken }),
          cache: "no-store",
        });
        if (!response.ok) {
          if (response.status === 401) delete token.backendAuth;
          return token;
        }

        const result = await response.json() as Partial<AuthResponse>;
        const nextRefreshToken = result.refreshToken || readRefreshCookie(response.headers.get("set-cookie"));
        if (!result.accessToken || !result.accessTokenExpiresAt || !nextRefreshToken) {
          delete token.backendAuth;
          return token;
        }
        token.backendAuth = { ...backendAuth, ...result, refreshToken: nextRefreshToken };
      } catch {
        // Keep the session for a later retry if the API is temporarily unavailable.
      }
      return token;
    },
    async session({ session, token }) {
      const backendAuth = token.backendAuth as AuthResponse | undefined;
      if (backendAuth) {
        const { refreshToken: _refreshToken, ...clientAuth } = backendAuth;
        session.backendAuth = clientAuth;
      }
      return session;
    },
  },
});

function readRefreshCookie(setCookie: string | null): string | undefined {
  if (!setCookie) return undefined;

  const cookiePair = setCookie.split(";", 1)[0];
  const prefix = "culinaryblog_refresh=";
  if (!cookiePair.startsWith(prefix)) return undefined;

  return decodeURIComponent(cookiePair.slice(prefix.length));
}
