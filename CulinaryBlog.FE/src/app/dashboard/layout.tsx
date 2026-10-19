"use client";

import Link from "next/link";
import { useEffect, useState, type ReactNode } from "react";
import { DashboardSidebar } from "../../components/layout/DashboardSidebar";
import { Footer } from "../../components/layout/Footer";
import { AUTH_CHANGED_EVENT, readAuthSession, type AuthSession } from "../../lib/auth-session";

export default function DashboardLayout({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const sync = () => { setSession(readAuthSession()); setReady(true); };
    sync();
    window.addEventListener("storage", sync);
    window.addEventListener(AUTH_CHANGED_EVENT, sync);
    return () => {
      window.removeEventListener("storage", sync);
      window.removeEventListener(AUTH_CHANGED_EVENT, sync);
    };
  }, []);

  if (!ready) return (
    <>
      <main className="min-h-[60vh] p-8">Đang kiểm tra tài khoản...</main>
      <Footer />
    </>
  );
  if (session?.role !== "author" && session?.role !== "admin") {
    return (
      <>
        <main className="mx-auto min-h-[60vh] max-w-2xl space-y-4 p-8">
          <h1 className="font-editorial text-2xl font-bold">Cần tài khoản tác giả hoặc quản trị viên</h1>
          <p>Vui lòng đăng nhập để mở dashboard.</p>
          <Link href="/auth/login" className="inline-block rounded-lg bg-[#063C2F] px-4 py-2 text-white">Đăng nhập</Link>
        </main>
        <Footer />
      </>
    );
  }

  return (
    <div className="flex min-h-[calc(100vh-72px)] flex-col bg-[#F5F0E6] md:h-[calc(100dvh-72px)] md:min-h-0 md:flex-row md:overflow-hidden">
      <DashboardSidebar />
      <main className="min-w-0 flex-1 md:min-h-0 md:overflow-y-auto">
        <div className="px-5 py-8 sm:px-8">{children}</div>
        <Footer />
      </main>
    </div>
  );
}
