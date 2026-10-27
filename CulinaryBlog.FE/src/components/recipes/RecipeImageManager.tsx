"use client";

import { useState } from "react";
import { authHeaders, recipeImageUrl, recipeRequest, type RecipeImage } from "../../lib/recipes";
import { ensureFreshSession } from "../../lib/auth-session";

export function RecipeImageManager({ recipeId, images, onChanged }: {
  recipeId: string;
  images: RecipeImage[];
  onChanged: () => Promise<void>;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [confirmId, setConfirmId] = useState<string | null>(null);

  async function run(action: () => Promise<unknown>) {
    if (!await ensureFreshSession()) {
      setError("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại trước khi quản lý ảnh.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      await action();
      await onChanged();
      setConfirmId(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể cập nhật ảnh công thức.");
    } finally {
      setBusy(false);
    }
  }

  async function upload() {
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      setError("Ảnh không được vượt quá 5 MB.");
      return;
    }
    await run(async () => {
      const data = new FormData();
      data.append("file", file);
      data.append("isPrimary", String(images.length === 0));
      await recipeRequest(`/v1/recipes/${recipeId}/images/`, { method: "POST", headers: authHeaders(), body: data });
      setFile(null);
    });
  }

  return <section className="space-y-4 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm sm:p-8">
    <div><h2 className="font-editorial text-lg font-bold">Ảnh công thức</h2><p className="text-xs text-[#66726B]">Tải thêm ảnh, chọn ảnh chính hoặc xóa ảnh không dùng nữa.</p></div>
    {error && <p role="alert" className="rounded-lg bg-[#B9674A]/10 p-3 text-xs text-[#8D3C25]">{error}</p>}
    <div className="flex flex-wrap items-end gap-3"><label className="flex-1 text-xs font-semibold text-[#4B5750]">Tải ảnh mới<input type="file" accept="image/jpeg,image/png,image/webp,image/avif" onChange={event => setFile(event.target.files?.[0] ?? null)} className="mt-1 block w-full rounded-lg border border-[#E5DECE] p-2" /></label><button type="button" disabled={!file || busy} onClick={upload} className="rounded-lg bg-[#063C2F] px-4 py-2.5 text-xs font-semibold text-white disabled:opacity-40">Tải lên</button></div>
    {images.length === 0 ? <p className="text-xs text-[#66726B]">Chưa có ảnh nào.</p> : <div className="grid gap-4 sm:grid-cols-2">{images.map(image => <div key={image.imageId} className="overflow-hidden rounded-xl border border-[#E5DECE] bg-[#F5F0E6]"><img src={recipeImageUrl(image.thumbnailUrl ?? image.originalUrl) ?? ""} alt={image.altText || "Ảnh công thức"} className="aspect-video w-full object-cover" /><div className="space-y-2 p-3"><p className="text-xs font-semibold text-[#063C2F]">{image.isPrimary ? "Ảnh chính" : "Ảnh phụ"}</p><div className="flex flex-wrap gap-2">{!image.isPrimary && <button type="button" disabled={busy} onClick={() => run(() => recipeRequest(`/v1/recipes/${recipeId}/images/${image.imageId}`, { method: "PATCH", headers: { ...authHeaders(), "Content-Type": "application/json" }, body: JSON.stringify({ isPrimary: true }) }))} className="rounded border border-[#C6A15B] px-2 py-1 text-xs disabled:opacity-40">Đặt làm ảnh chính</button>}<button type="button" disabled={busy} onClick={() => setConfirmId(image.imageId)} className="rounded border border-[#B9674A]/40 px-2 py-1 text-xs text-[#8D3C25] disabled:opacity-40">Xóa ảnh</button></div>{confirmId === image.imageId && <div className="flex items-center gap-2 text-xs"><span>Xóa ảnh này?</span><button type="button" onClick={() => setConfirmId(null)} className="underline">Hủy</button><button type="button" disabled={busy} onClick={() => run(() => recipeRequest(`/v1/recipes/${recipeId}/images/${image.imageId}`, { method: "DELETE", headers: authHeaders() }))} className="font-semibold text-[#8D3C25] disabled:opacity-40">Xác nhận</button></div>}</div></div>)}</div>}
  </section>;
}
