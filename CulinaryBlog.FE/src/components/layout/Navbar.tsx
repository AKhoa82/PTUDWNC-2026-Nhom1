
"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  ChefHat,
  Search,
  Menu,
  X,
  Bookmark,
  PlusCircle,
  LayoutDashboard,
  UserRound,
} from "lucide-react";

interface NavbarProps {
  isLoggedIn?: boolean;
  userName?: string;
  userRole?: "admin" | "author" | "reader";
  savedCount?: number;
}

const navLinks = [
  { label: "Trang chủ", href: "/" },
  { label: "Công thức", href: "/recipes" },
  { label: "Danh mục", href: "/categories" },
];

export function Navbar({
  isLoggedIn = false,
  userName = "Tài khoản",
  userRole = "reader",
  savedCount = 0,
}: NavbarProps) {
  const pathname = usePathname();
  const router = useRouter();

  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");

  const canManage =
    isLoggedIn &&
    (userRole === "admin" || userRole === "author");

  const isActive = (href: string) =>
    href === "/"
      ? pathname === "/"
      : pathname === href || pathname.startsWith(href + "/");

  const handleSearch = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();

    const keyword = searchQuery.trim();

    if (!keyword) return;

    router.push(`/search?q=${encodeURIComponent(keyword)}`);
    setMobileMenuOpen(false);
  };

  return (
    <header className="sticky top-0 z-50 border-b border-[#063C2F] bg-[#022C24]/95 text-[#FBF8F1] shadow-md backdrop-blur-md">

      <div className="mx-auto flex h-[72px] max-w-7xl items-center justify-between gap-4 px-4 sm:px-6 lg:px-8">

        {/* LOGO */}
        <Link
          href="/"
          onClick={() => setMobileMenuOpen(false)}
          className="group flex shrink-0 items-center gap-3"
        >
          <div className="flex h-10 w-10 items-center justify-center rounded-lg border border-[#C6A15B]/30 bg-[#063C2F] transition-colors group-hover:border-[#C6A15B]">
            <ChefHat className="h-5 w-5 text-[#C6A15B]" />
          </div>

          <div>
            <span className="block font-editorial text-xl font-bold leading-none tracking-tight">
              Culinary
              <span className="font-normal italic text-[#C6A15B]">
                Blog
              </span>
            </span>

            <span className="mt-1 block text-[9px] font-medium uppercase tracking-[0.2em] text-[#C6A15B]">
              Bản Sắc Ẩm Thực Việt
            </span>
          </div>
        </Link>

        {/* MENU DESKTOP */}
        <nav className="hidden items-center gap-1 md:flex">
          {navLinks.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              aria-current={
                isActive(link.href) ? "page" : undefined
              }
              className={`rounded-md px-3 py-2 text-xs font-semibold uppercase tracking-wider transition-all ${
                isActive(link.href)
                  ? "border border-[#C6A15B]/30 bg-[#063C2F] text-[#C6A15B]"
                  : "text-[#F5F0E6]/75 hover:bg-[#063C2F] hover:text-[#FBF8F1]"
              }`}
            >
              {link.label}
            </Link>
          ))}
        </nav>

        {/* TÌM KIẾM DESKTOP */}
        <form
          onSubmit={handleSearch}
          className="relative hidden max-w-[250px] flex-1 lg:block"
          role="search"
        >
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#C6A15B]" />

          <input
            type="search"
            placeholder="Tìm món ngon, nguyên liệu..."
            aria-label="Tìm kiếm công thức"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full rounded-full border border-[#24604B] bg-[#062E27] py-2 pl-9 pr-4 text-xs text-white outline-none placeholder:text-white/40 focus:border-[#C6A15B]"
          />
        </form>

        {/* ACTION DESKTOP */}
        <div className="hidden shrink-0 items-center gap-2 md:flex">

          <Link
            href="/search"
            aria-label="Tìm kiếm"
            className="rounded-lg p-2 hover:bg-[#063C2F] lg:hidden"
          >
            <Search className="h-5 w-5" />
          </Link>

          {isLoggedIn ? (
            <>
              <Link
                href="/profile"
                title="Công thức đã lưu"
                className="relative rounded-lg p-2 text-[#F5F0E6]/80 hover:text-[#C6A15B]"
              >
                <Bookmark className="h-5 w-5" />

                {savedCount > 0 && (
                  <span className="absolute -right-1 -top-1 rounded-full bg-[#B9674A] px-1.5 text-[10px] text-white">
                    {savedCount}
                  </span>
                )}
              </Link>

              {canManage && (
                <>
                  <Link
                    href="/dashboard"
                    className="hidden items-center gap-1.5 rounded-lg border border-[#24604B] px-3 py-2 text-xs hover:border-[#C6A15B] xl:flex"
                  >
                    <LayoutDashboard className="h-4 w-4 text-[#C6A15B]" />
                    Dashboard
                  </Link>

                  <Link
                    href="/dashboard/recipes/new"
                    className="inline-flex items-center gap-1.5 rounded-lg bg-[#C6A15B] px-3 py-2 text-xs font-semibold text-[#022C24] transition-colors hover:bg-[#B8934E]"
                  >
                    <PlusCircle className="h-4 w-4" />
                    Tạo công thức
                  </Link>
                </>
              )}

              <Link
                href="/profile"
                className="flex items-center gap-2 rounded-full border border-[#C6A15B]/30 px-3 py-2 text-xs hover:bg-[#063C2F]"
              >
                <UserRound className="h-4 w-4 text-[#C6A15B]" />
                <span className="hidden xl:inline">
                  {userName}
                </span>
              </Link>
            </>
          ) : (
            <>
              <Link
                href="/auth/login"
                className="px-3 py-2 text-xs font-semibold text-[#F5F0E6] hover:text-[#C6A15B]"
              >
                Đăng nhập
              </Link>

              <Link
                href="/auth/register"
                className="rounded-lg bg-[#C6A15B] px-3 py-2 text-xs font-semibold text-[#022C24] hover:bg-[#B8934E]"
              >
                Đăng ký
              </Link>
            </>
          )}
        </div>

        {/* NÚT MENU MOBILE */}
        <div className="flex items-center gap-1 md:hidden">
          <Link
            href="/search"
            aria-label="Tìm kiếm"
            className="rounded-lg p-2 hover:bg-[#063C2F]"
          >
            <Search className="h-5 w-5" />
          </Link>

          <button
            type="button"
            aria-label={mobileMenuOpen ? "Đóng menu" : "Mở menu"}
            aria-expanded={mobileMenuOpen}
            aria-controls="culinary-mobile-menu"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            className="rounded-lg p-2 hover:bg-[#063C2F]"
          >
            {mobileMenuOpen ? (
              <X className="h-6 w-6" />
            ) : (
              <Menu className="h-6 w-6" />
            )}
          </button>
        </div>
      </div>

      {/* MENU MOBILE */}
      {mobileMenuOpen && (
        <div
          id="culinary-mobile-menu"
          className="border-t border-[#063C2F] bg-[#022C24] px-4 py-5 md:hidden"
        >
          <form onSubmit={handleSearch} className="relative mb-4">
            <Search className="absolute left-3 top-3 h-4 w-4 text-[#C6A15B]" />

            <input
              type="search"
              placeholder="Tìm kiếm công thức..."
              aria-label="Tìm kiếm trên điện thoại"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full rounded-lg border border-[#24604B] bg-[#062E27] py-2.5 pl-10 pr-3 text-sm text-white outline-none"
            />
          </form>

          <nav className="space-y-1">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMobileMenuOpen(false)}
                className={`block rounded-lg px-4 py-3 text-xs font-semibold uppercase tracking-wider ${
                  isActive(link.href)
                    ? "bg-[#063C2F] text-[#C6A15B]"
                    : "text-[#F5F0E6]/80 hover:bg-[#063C2F]"
                }`}
              >
                {link.label}
              </Link>
            ))}
          </nav>

          <div className="mt-4 flex flex-col gap-2 border-t border-[#063C2F] pt-4">
            {isLoggedIn ? (
              <>
                <Link
                  href="/profile"
                  onClick={() => setMobileMenuOpen(false)}
                  className="rounded-lg border border-[#24604B] px-4 py-3 text-sm"
                >
                  Hồ sơ cá nhân
                </Link>

                {canManage && (
                  <>
                    <Link
                      href="/dashboard"
                      onClick={() => setMobileMenuOpen(false)}
                      className="rounded-lg border border-[#24604B] px-4 py-3 text-sm"
                    >
                      Dashboard
                    </Link>
                    <Link
                      href="/dashboard/recipes/new"
                      onClick={() => setMobileMenuOpen(false)}
                      className="rounded-lg bg-[#C6A15B] px-4 py-3 text-center text-sm font-semibold text-[#022C24]"
                    >
                      Tạo công thức
                    </Link>
                  </>
                )}
              </>
            ) : (
              <>
                <Link
                  href="/auth/login"
                  onClick={() => setMobileMenuOpen(false)}
                  className="rounded-lg border border-[#24604B] px-4 py-3 text-center text-sm"
                >
                  Đăng nhập
                </Link>

                <Link
                  href="/auth/register"
                  onClick={() => setMobileMenuOpen(false)}
                  className="rounded-lg bg-[#C6A15B] px-4 py-3 text-center text-sm font-semibold text-[#022C24]"
                >
                  Đăng ký
                </Link>
              </>
            )}
          </div>
        </div>
      )}
    </header>
  );
}
