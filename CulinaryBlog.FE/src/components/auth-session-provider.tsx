"use client";

import { SessionProvider, useSession } from "next-auth/react";
import { useEffect, type ReactNode } from "react";
import { ensureFreshSession, saveAuthSession } from "@/lib/auth-session";
import { clearAuth } from "@/lib/auth-client";

function SessionCoordinator({ children }: { children: ReactNode }) {
  const { data: session, status } = useSession();

  useEffect(() => {
    if (status === "loading" || localStorage.getItem("authProvider") !== "google") return;
    const auth = session?.backendAuth;
    if (!auth) {
      clearAuth();
    } else if (auth.accessToken !== localStorage.getItem("accessToken")) {
      saveAuthSession(auth, "google");
    }
  }, [session, status]);

  useEffect(() => {
    if (status === "loading" && localStorage.getItem("authProvider") === "google") return;
    const refresh = () => { void ensureFreshSession(); };
    refresh();
    const timer = window.setInterval(refresh, 30_000);
    window.addEventListener("focus", refresh);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, [status]);

  return children;
}

export function AuthSessionProvider({ children }: { children: ReactNode }) {
  return <SessionProvider refetchOnWindowFocus={false}><SessionCoordinator>{children}</SessionCoordinator></SessionProvider>;
}
