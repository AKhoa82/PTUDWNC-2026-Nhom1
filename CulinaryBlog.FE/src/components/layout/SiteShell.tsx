
"use client";

import { useEffect, useState, type ReactNode } from "react";
import { usePathname } from "next/navigation";
import { AUTH_CHANGED_EVENT, readAuthSession, type AuthSession } from "../../lib/auth-session";

import { Navbar } from "./Navbar";
import { Footer } from "./Footer";

interface SiteShellProps {
  children: ReactNode;
}

export function SiteShell({ children }: SiteShellProps) {
  const pathname = usePathname();
  const [session, setSession] = useState<AuthSession | null>(null);

  useEffect(() => {
    const syncSession = () => setSession(readAuthSession());
    syncSession();
    window.addEventListener("storage", syncSession);
    window.addEventListener(AUTH_CHANGED_EVENT, syncSession);
    return () => {
      window.removeEventListener("storage", syncSession);
      window.removeEventListener(AUTH_CHANGED_EVENT, syncSession);
    };
  }, [pathname]);

  const isAuthPage = pathname.startsWith("/auth/");

  if (isAuthPage) {
    return <>{children}</>;
  }

  return (
    <div className="flex min-h-screen flex-col bg-[#FBF8F1]">

      {/* HEADER */}
      <Navbar isLoggedIn={Boolean(session)} userName={session?.name} userRole={session?.role} />

      {/* NỘI DUNG TRANG */}
      <div className="min-w-0 flex-1">
        {children}
      </div>

      {/* FOOTER */}
      <Footer />

    </div>
  );
}
