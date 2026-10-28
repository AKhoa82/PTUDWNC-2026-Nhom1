"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import {
  CircleAlert,
  ImagePlus,
  LoaderCircle,
  Save,
  UserRound,
  X,
} from "lucide-react";

import {
  AUTH_CHANGED_EVENT,
  fetchWithAuth,
} from "@/lib/auth-session";

import type { UserProfile } from "@/types/user-profile";

const apiUrl = (
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:5018/api"
).replace(/\/$/, "");

interface ProfileEditFormProps {
  profile: UserProfile;
  onSaved: (profile: UserProfile) => void;
  onCancel: () => void;
}

interface ApiProblem {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

function isValidImageUrl(value: string): boolean {
  try {
    const url = new URL(value);

    return (
      (url.protocol === "http:" ||
        url.protocol === "https:") &&
      !url.username &&
      !url.password &&
      value.length <= 500
    );
  } catch {
    return false;
  }
}

export function ProfileEditForm({
  profile,
  onSaved,
  onCancel,
}: ProfileEditFormProps) {
  const router = useRouter();

  const [fullName, setFullName] = useState(profile.fullName);
  const [avatarUrl, setAvatarUrl] = useState(
    profile.avatarUrl ?? ""
  );

  const [previewFailed, setPreviewFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const previewUrl = avatarUrl.trim();

  const showPreview =
    isValidImageUrl(previewUrl) && !previewFailed;

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();
    if (saving) return;

    setError("");

    const name = fullName.trim();
    const avatar = avatarUrl.trim();

    if (name.length < 2 || name.length > 100) {
      setError("Họ tên phải có từ 2 đến 100 ký tự.");
      return;
    }

    if (avatar && !isValidImageUrl(avatar)) {
      setError("URL ảnh đại diện không hợp lệ.");
      return;
    }

    const changes: {
      fullName?: string;
      avatarUrl?: string | null;
    } = {};

    if (name !== profile.fullName) {
      changes.fullName = name;
    }

    if (avatar !== (profile.avatarUrl ?? "")) {
      changes.avatarUrl = avatar || null;
    }

    if (Object.keys(changes).length === 0) {
      onCancel();
      return;
    }

    const token = localStorage.getItem("accessToken");

    if (!token) {
      router.replace("/auth/login");
      return;
    }

    setSaving(true);

    try {
      const response = await fetchWithAuth(
        `${apiUrl}/v1/auth/me`,
        {
          method: "PATCH",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify(changes),
          cache: "no-store",
        }
      );

      if (response.status === 401) {
        router.replace("/auth/login");
        return;
      }

      if (!response.ok) {
        const problem = (await response
          .json()
          .catch(() => null)) as ApiProblem | null;

        const validationMessage = problem?.errors
          ? Object.values(problem.errors).flat()[0]
          : undefined;

        throw new Error(
          validationMessage ??
            problem?.detail ??
            "Không thể cập nhật hồ sơ cá nhân."
        );
      }

      const updatedProfile =
        (await response.json()) as UserProfile;

      // Đồng bộ tên hiển thị ở Navbar.
      localStorage.setItem(
        "userName",
        updatedProfile.fullName || updatedProfile.userName
      );

      window.dispatchEvent(
        new Event(AUTH_CHANGED_EVENT)
      );

      onSaved(updatedProfile);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Đã xảy ra lỗi khi cập nhật hồ sơ."
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-6 rounded-xl border border-[#C6A15B]/40 bg-[#F5F0E6] p-5 sm:p-6"
    >
      <div>
        <p className="text-sm font-semibold text-[#022C24]">
          Chỉnh sửa thông tin cá nhân
        </p>
        <p className="mt-1 text-xs text-[#66726B]">
          Bạn có thể thay đổi họ tên và ảnh đại diện.
        </p>
      </div>

      {/* Ảnh đại diện */}
      <div className="space-y-3">
        <label className="block text-xs font-semibold text-[#1C2421]">
          Ảnh đại diện
        </label>

        <div className="flex flex-wrap items-center gap-5">
          <div className="flex h-24 w-24 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-[#E5DECE] bg-white">
            {showPreview ? (
              <img
                src={previewUrl}
                alt="Xem trước ảnh đại diện"
                className="h-full w-full object-cover"
                onError={() => setPreviewFailed(true)}
              />
            ) : (
              <UserRound className="h-10 w-10 text-[#8D958F]" />
            )}
          </div>

          <div className="min-w-0 flex-1 space-y-2">
            <div className="flex items-center gap-2 text-xs font-semibold text-[#66726B]">
              <ImagePlus className="h-4 w-4 text-[#A8894D]" />
              Đường dẫn ảnh đại diện
            </div>

            <input
              type="url"
              value={avatarUrl}
              onChange={(event) => {
                setAvatarUrl(event.target.value);
                setPreviewFailed(false);
              }}
              placeholder="https://example.com/avatar.jpg"
              maxLength={500}
              disabled={saving}
              className="w-full rounded-lg border border-[#E5DECE] bg-white px-3 py-2.5 text-sm text-[#1C2421] outline-none transition-colors focus:border-[#A8894D] disabled:opacity-60"
            />

            <button
              type="button"
              onClick={() => {
                setAvatarUrl("");
                setPreviewFailed(false);
              }}
              disabled={saving || !avatarUrl}
              className="text-xs font-medium text-[#B9674A] hover:underline disabled:opacity-40"
            >
              Xóa ảnh đại diện
            </button>
          </div>
        </div>
      </div>

      {/* Họ và tên */}
      <div className="space-y-2">
        <label
          htmlFor="profile-full-name"
          className="block text-xs font-semibold text-[#1C2421]"
        >
          Họ và tên
          <span className="ml-1 text-[#B9674A]">*</span>
        </label>

        <input
          id="profile-full-name"
          type="text"
          value={fullName}
          onChange={(event) =>
            setFullName(event.target.value)
          }
          minLength={2}
          maxLength={100}
          required
          disabled={saving}
          placeholder="Nhập họ và tên"
          className="w-full rounded-lg border border-[#E5DECE] bg-white px-4 py-3 text-sm text-[#1C2421] outline-none transition-colors focus:border-[#A8894D] disabled:opacity-60"
        />

        <p className="text-right text-[11px] text-[#8D958F]">
          {fullName.length}/100 ký tự
        </p>
      </div>

      {/* Thông tin không được sửa */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <label className="block text-xs font-semibold text-[#66726B]">
            Email
          </label>

          <input
            type="text"
            value={profile.email}
            disabled
            className="w-full cursor-not-allowed rounded-lg border border-[#E5DECE] bg-[#EAE5DA] px-4 py-3 text-sm text-[#8D958F]"
          />
        </div>

        <div className="space-y-2">
          <label className="block text-xs font-semibold text-[#66726B]">
            Tên đăng nhập
          </label>

          <input
            type="text"
            value={profile.userName}
            disabled
            className="w-full cursor-not-allowed rounded-lg border border-[#E5DECE] bg-[#EAE5DA] px-4 py-3 text-sm text-[#8D958F]"
          />
        </div>
      </div>

      <p className="text-xs leading-6 text-[#8D958F]">
        Email và tên đăng nhập không thể thay đổi
        tại đây để bảo vệ tài khoản của bạn.
      </p>

      {error && (
        <div
          role="alert"
          className="flex items-start gap-2 rounded-lg border border-[#B9674A]/30 bg-[#B9674A]/10 px-4 py-3 text-sm text-[#9B4A36]"
        >
          <CircleAlert className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Nút thao tác */}
      <div className="flex flex-wrap justify-end gap-3 border-t border-[#E5DECE] pt-5">
        <button
          type="button"
          onClick={onCancel}
          disabled={saving}
          className="inline-flex items-center gap-2 rounded-lg border border-[#E5DECE] bg-white px-5 py-2.5 text-sm font-semibold text-[#66726B] hover:bg-[#EAE5DA] disabled:opacity-50"
        >
          <X className="h-4 w-4" />
          Hủy bỏ
        </button>

        <button
          type="submit"
          disabled={saving}
          className="inline-flex items-center gap-2 rounded-lg bg-[#022C24] px-5 py-2.5 text-sm font-semibold text-[#FBF8F1] transition-colors hover:bg-[#063C2F] disabled:cursor-not-allowed disabled:opacity-60"
        >
          {saving ? (
            <>
              <LoaderCircle className="h-4 w-4 animate-spin" />
              Đang lưu...
            </>
          ) : (
            <>
              <Save className="h-4 w-4 text-[#C6A15B]" />
              Lưu thay đổi
            </>
          )}
        </button>
      </div>
    </form>
  );
}
