
"use client";

import Link from "next/link";
import Image from "next/image";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { AUTH_CHANGED_EVENT, readAuthSession, type AuthSession } from "../../lib/auth-session";
import {
  LayoutDashboard,
  UtensilsCrossed,
  PlusCircle,
  FolderTree,
  UserRound,
  ShieldCheck,
  ChevronRight,
  ExternalLink,
} from "lucide-react";

interface DashboardSidebarProps {
  userName?: string;
  roleLabel?: string;
}

const menuItems = [
  {
    label: "Tổng quan Dashboard",
    href: "/dashboard",
    icon: LayoutDashboard,
  },
  {
    label: "Quản lý công thức",
    href: "/dashboard/recipes",
    icon: UtensilsCrossed,
  },
  {
    label: "Thêm công thức mới",
    href: "/dashboard/recipes/new",
    icon: PlusCircle,
  },
  {
    label: "Quản lý danh mục",
    href: "/dashboard/categories",
    icon: FolderTree,
  },
  {
    label: "Hồ sơ cá nhân",
    href: "/profile",
    icon: UserRound,
  },
];

export function DashboardSidebar({
  userName,
  roleLabel,
}: DashboardSidebarProps) {
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
  }, []);

  const displayName = userName ?? session?.name ?? "Tác giả";
  const displayRole = roleLabel ?? (session?.role === "admin" ? "Quản trị viên" : "Tài khoản tác giả");

  return (
    <aside className="flex w-full flex-col justify-between border-b border-[#E5DECE] bg-[#FBF8F1] p-4 md:min-h-[calc(100vh-72px)] md:w-64 md:shrink-0 md:border-b-0 md:border-r">

      <div className="space-y-6">

        {/* THÔNG TIN NGƯỜI DÙNG */}
        <div className="flex items-center gap-3 rounded-xl border border-[#E5DECE] bg-[#F5F0E6] p-3">

          <Image
            src="/images/dashboard-avatar.jpg"
            alt="Ảnh đại diện mẫu"
            width={40}
            height={40}
            className="h-10 w-10 shrink-0 rounded-full object-cover ring-2 ring-[#C6A15B]/30"
          />

          <div className="min-w-0">
            <h3 className="truncate font-editorial text-sm font-bold text-[#1C2421]">
              {displayName}
            </h3>

            <p className="mt-1 flex items-center gap-1 text-[10px] font-semibold uppercase text-[#063C2F]">
              <ShieldCheck className="h-3 w-3 text-[#C6A15B]" />
              {displayRole}
            </p>
          </div>
        </div>

        {/* MENU */}
        <nav aria-label="Quản trị nội dung" className="space-y-1">

          <p className="px-3 pb-2 text-[10px] font-bold uppercase tracking-[0.2em] text-[#A8894D]">
            Quản trị nội dung
          </p>

          {menuItems.map((item) => {
            const Icon = item.icon;
            const active = pathname === item.href;

            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={`
                  flex items-center justify-between
                  rounded-lg px-3 py-3
                  text-xs font-semibold transition-all
                  ${
                    active
                      ? "border border-[#C6A15B]/40 bg-[#022C24] text-[#FBF8F1]"
                      : "text-[#1C2421]/80 hover:bg-[#F5F0E6] hover:text-[#063C2F]"
                  }
                `}
              >
                <span className="flex items-center gap-3">
                  <Icon
                    className={`h-4 w-4 ${
                      active
                        ? "text-[#C6A15B]"
                        : "text-[#8D958F]"
                    }`}
                  />

                  {item.label}
                </span>

                {active && (
                  <ChevronRight className="h-4 w-4 text-[#C6A15B]" />
                )}
              </Link>
            );
          })}

        </nav>
      </div>

      {/* QUAY VỀ TRANG CÔNG THỨC */}
      <div className="mt-6 border-t border-[#E5DECE] pt-4">
        <Link
          href="/recipes"
          className="flex items-center justify-center gap-2 rounded-lg px-3 py-3 text-xs font-medium text-[#1C2421]/80 hover:bg-[#F5F0E6] hover:text-[#063C2F]"
        >
          <ExternalLink className="h-4 w-4 text-[#A8894D]" />
          Về trang công thức
        </Link>
      </div>

    </aside>
  );
}
