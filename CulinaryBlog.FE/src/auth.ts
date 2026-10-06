import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
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
      if (account?.provider !== "google") return token;
      if (!account.id_token) throw new Error("Google did not return an ID token.");

      const response = await fetch(`${apiBaseUrl}/v1/auth/google`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ idToken: account.id_token }),
        cache: "no-store",
      });

      const result = await response.json().catch(() => null) as
        | { message?: string; user?: AuthResponse }
        | null;

      if (!response.ok || !result?.user) {
        throw new Error(result?.message ?? "The API could not complete Google sign-in.");
      }

      token.backendAuth = result.user;
      return token;
    },
    async session({ session, token }) {
      session.backendAuth = token.backendAuth as AuthResponse | undefined;
      return session;
    },
  },
});
