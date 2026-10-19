import type { AuthResponse } from "@/lib/auth-client";

declare module "next-auth" {
  interface Session {
    backendAuth?: AuthResponse;
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    backendAuth?: AuthResponse;
  }
}
