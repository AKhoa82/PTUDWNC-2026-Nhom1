"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  AtSign,
  CalendarDays,
  CheckCircle2,
  CircleAlert,
  LayoutDashboard,
  Mail,
  RefreshCcw,
  ShieldCheck,
  UserRound,
} from "lucide-react";

import {
  fetchWithAuth,
} from "@/lib/auth-session";

import type {
  UserProfile,
} from "@/types/user-profile";

const apiUrl = (
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:5018/api"
).replace(/\/$/, "");

function formatDate(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Chưa xác định";
  }

  return new Intl.DateTimeFormat("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(date);
}

function InfoItem({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode;
  label: string;
  value: React.ReactNode;
}) {
  return (
    <div className="flex items-start gap-3 rounded-xl border border-[#E5DECE] bg-[#F5F0E6]/60 p-4">
      <div className="rounded-lg bg-[#022C24] p-2.5 text-[#C6A15B]">
        {icon}
      </div>

      <div className="min-w-0 flex-1">
        <p className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-[#8D958F]">
          {label}
        </p>
        <div className="break-words text-sm font-medium text-[#1C2421]">
          {value}
        </div>
      </div>
    </div>
  );
}

export default function ProfilePage() {
  const router = useRouter();

  const [profile, setProfile] =
    useState<UserProfile | null>(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const [avatarFailed, setAvatarFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadProfile() {
      setLoading(true);
      setError("");

      const token = localStorage.getItem("accessToken");

      if (!token) {
        router.replace("/auth/login");
        return;
      }

      try {
        const response = await fetchWithAuth(
          `${apiUrl}/v1/auth/me`,
          {
            method: "GET",
            headers: {
              Authorization: `Bearer ${token}`,
            },
            cache: "no-store",
          }
        );

        if (response.status === 401) {
          if (!cancelled) {
            router.replace("/auth/login");
          }
          return;
        }

        if (response.status === 403) {
          throw new Error(
            "Tài khoản không có quyền xem hồ sơ này."
          );
        }

        if (response.status === 404) {
          throw new Error(
            "Không tìm thấy hồ sơ người dùng."
          );
        }

        if (!response.ok) {
          throw new Error(
            "Không thể tải hồ sơ cá nhân."
          );
        }

        const data =
          (await response.json()) as UserProfile;

        if (!cancelled) {
          setProfile(data);
          setAvatarFailed(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error
              ? err.message
              : "Có lỗi xảy ra."
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadProfile();

    return () => {
      cancelled = true;
    };
  }, [router, reloadKey]);

  if (loading) {
    return (
      <main className="mx-auto max-w-5xl px-4 py-12">
        <div className="animate-pulse overflow-hidden rounded-2xl border border-[#E5DECE]">
          <div className="h-40 bg-[#022C24]" />
          <div className="space-y-4 bg-[#FBF8F1] p-8">
            <div className="h-20 w-20 rounded-xl bg-[#E5DECE]" />
            <div className="h-5 w-48 rounded bg-[#E5DECE]" />
            <div className="h-4 w-72 rounded bg-[#E5DECE]" />
          </div>
        </div>
        <p className="mt-4 text-center text-sm text-[#8D958F]">
          Đang tải hồ sơ cá nhân...
        </p>
      </main>
    );
  }

  if (error) {
    return (
      <main className="mx-auto max-w-2xl px-4 py-20 text-center">
        <CircleAlert className="mx-auto mb-4 h-10 w-10 text-[#B9674A]" />
        <h1 className="font-editorial text-2xl font-bold text-[#1C2421]">
          Không thể hiển thị hồ sơ
        </h1>
        <p className="mt-3 text-sm text-[#8D958F]">
          {error}
        </p>
        <button
          type="button"
          onClick={() => setReloadKey((n) => n + 1)}
          className="mt-6 inline-flex items-center gap-2 rounded-lg bg-[#022C24] px-5 py-3 text-sm font-semibold text-[#FBF8F1] hover:bg-[#063C2F]"
        >
          <RefreshCcw className="h-4 w-4" />
          Thử lại
        </button>
      </main>
    );
  }

  if (!profile) return null;

  const isAdmin = profile.roles.some(
    (role) => role.toLowerCase() === "admin"
  );

  const isAuthor = profile.roles.some(
    (role) => role.toLowerCase() === "author"
  );

  const displayName =
    profile.fullName || profile.userName;

  return (
    <main className="mx-auto w-full max-w-5xl space-y-7 px-4 py-8 pb-20 sm:px-6">
      <div>
        <Link
          href="/"
          className="inline-flex items-center gap-2 text-xs font-semibold text-[#8D958F] transition-colors hover:text-[#063C2F]"
        >
          <ArrowLeft className="h-4 w-4" />
          Quay về trang chủ
        </Link>

        <p className="mt-6 text-[11px] font-semibold uppercase tracking-[0.24em] text-[#A8894D]">
          Không gian cá nhân
        </p>

        <h1 className="mt-1 font-editorial text-3xl font-bold text-[#1C2421]">
          Hồ sơ của tôi
        </h1>
      </div>

      <section className="overflow-hidden rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] shadow-sm">
        <div className="relative h-36 border-b border-[#063C2F] bg-[#022C24] sm:h-44">
          <div className="absolute inset-0 opacity-10 [background-image:radial-gradient(#C6A15B_1px,transparent_1px)] [background-size:16px_16px]" />
        </div>

        <div className="relative px-6 pb-8 sm:px-8">
          <div className="-mt-14 flex flex-col gap-5 sm:-mt-16 sm:flex-row sm:items-end sm:justify-between">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
              <div className="flex h-28 w-28 shrink-0 items-center justify-center overflow-hidden rounded-xl border-4 border-[#FBF8F1] bg-[#E5DECE] shadow-md">
                {profile.avatarUrl && !avatarFailed ? (
                  <img
                    src={profile.avatarUrl}
                    alt={`Ảnh đại diện của ${displayName}`}
                    className="h-full w-full object-cover"
                    onError={() => setAvatarFailed(true)}
                  />
                ) : (
                  <UserRound className="h-12 w-12 text-[#8D958F]" />
                )}
              </div>

              <div className="min-w-0 space-y-2 sm:pb-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="font-editorial text-2xl font-bold text-[#1C2421]">
                    {displayName}
                  </h2>
                  <span className="inline-flex items-center gap-1 rounded-full border border-[#C6A15B]/30 bg-[#022C24] px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wider text-[#FBF8F1]">
                    <ShieldCheck className="h-3.5 w-3.5 text-[#C6A15B]" />
                    {isAdmin ? "Bếp trưởng quản trị" : isAuthor ? "Tác giả ẩm thực" : "Thành viên"}
                  </span>
                </div>
                <p className="break-words text-xs text-[#66726B]">
                  <span className="font-semibold text-[#1C2421]">@{profile.userName}</span>
                  <span className="mx-2" aria-hidden="true">•</span>
                  {profile.email}
                  {profile.emailConfirmed && (
                    <span className="ml-2 font-medium text-[#063C2F]">✓ Đã xác thực</span>
                  )}
                </p>
              </div>
            </div>

            {(isAuthor || isAdmin) && (
              <Link
                href="/dashboard"
                className="inline-flex items-center justify-center gap-2 rounded-lg bg-[#022C24] px-4 py-2.5 text-xs font-semibold text-[#FBF8F1] transition-colors hover:bg-[#063C2F]"
              >
                <LayoutDashboard className="h-4 w-4 text-[#C6A15B]" />
                Vào bàn làm việc
              </Link>
            )}
          </div>

          <div className="mt-6 flex flex-wrap items-center gap-5 border-t border-[#E5DECE] pt-4 text-xs text-[#66726B]">
            <span className="inline-flex items-center gap-2">
              <CalendarDays className="h-4 w-4 text-[#C6A15B]" />
              Gia nhập từ {formatDate(profile.createdAt)}
            </span>

            <span className="inline-flex items-center gap-2">
              {profile.emailConfirmed ? (
                <>
                  <CheckCircle2 className="h-4 w-4 text-[#063C2F]" />
                  Email đã xác thực
                </>
              ) : (
                <>
                  <CircleAlert className="h-4 w-4 text-[#B9674A]" />
                  Email chưa xác thực
                </>
              )}
            </span>
          </div>
        </div>
      </section>

      {/* Thông tin tài khoản */}
      <section className="rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm sm:p-8">
        <div className="mb-6 border-b border-[#E5DECE] pb-4">
          <h3 className="font-editorial text-xl font-bold text-[#1C2421]">
            Thông tin tài khoản
          </h3>
          <p className="mt-2 text-xs text-[#8D958F]">
            Những thông tin được cung cấp từ tài khoản của bạn.
          </p>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <InfoItem
            icon={<UserRound className="h-4 w-4" />}
            label="Họ và tên"
            value={displayName}
          />
          <InfoItem
            icon={<AtSign className="h-4 w-4" />}
            label="Tên đăng nhập"
            value={`@${profile.userName}`}
          />

          <InfoItem
            icon={<Mail className="h-4 w-4" />}
            label="Địa chỉ Email"
            value={profile.email}
          />

          <InfoItem
            icon={<ShieldCheck className="h-4 w-4" />}
            label="Vai trò hệ thống"
            value={profile.roles.join(", ") || "Chưa có vai trò"}
          />
        </div>
      </section>
    </main>
  );
}
