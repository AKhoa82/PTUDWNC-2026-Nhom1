"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Archive, ArchiveRestore, ArrowRight, Edit3, ExternalLink, FileText, PlusCircle, Search, Send, Trash2, UtensilsCrossed } from "lucide-react";
import { AUTH_CHANGED_EVENT, readAuthSession, type AuthSession } from "../../lib/auth-session";
import { authHeaders, difficultyLabels, formatRecipeDate, recipeImageUrl, recipeRequest, statusLabels, type PagedRecipes, type RecipeSummary } from "../../lib/recipes";

export function RecipeManager() {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [ready, setReady] = useState(false);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [notice, setNotice] = useState("");
  const queryClient = useQueryClient();
  useEffect(() => {
    const sync = () => { setSession(readAuthSession()); setReady(true); };
    sync(); window.addEventListener("storage", sync); window.addEventListener(AUTH_CHANGED_EVENT, sync);
    return () => { window.removeEventListener("storage", sync); window.removeEventListener(AUTH_CHANGED_EVENT, sync); };
  }, []);
  const allowed = session?.role === "admin" || session?.role === "author";
  const recipesQuery = useQuery({
    queryKey: ["managed-recipes", session?.id, session?.role, page, search, status],
    queryFn: () => {
      const params = new URLSearchParams({ page: String(page), pageSize: "50", sort: "-createdAt" });
      if (session?.role === "author") params.set("authorId", session.id);
      if (search.trim()) params.set("keyword", search.trim());
      if (status) params.set("status", status);
      return recipeRequest<PagedRecipes>(`/v1/recipes?${params}`, { headers: authHeaders(), cache: "no-store" });
    },
    enabled: ready && allowed,
    staleTime: 30_000,
  });
  const mutation = useMutation({
    mutationFn: async ({ id, action }: { id: string; action: "publish" | "unpublish" | "archive" | "unarchive" | "delete" }) => recipeRequest<void>(`/v1/recipes/${id}${action === "delete" ? "" : `/${action}`}`, { method: action === "delete" ? "DELETE" : "PATCH", headers: authHeaders() }),
    onSuccess: async (_, variables) => { setNotice(variables.action === "delete" ? "Đã xóa công thức." : "Đã cập nhật trạng thái công thức."); setConfirmId(null); await queryClient.invalidateQueries({ queryKey: ["managed-recipes"] }); await queryClient.invalidateQueries({ queryKey: ["recipe"] }); },
    onError: error => setNotice(error instanceof Error ? error.message : "Không thể cập nhật công thức."),
  });

  if (!ready) return <div className="mx-auto max-w-6xl p-8 text-sm text-[#66726B]">Đang kiểm tra tài khoản...</div>;
  if (!allowed) return <div className="mx-auto max-w-6xl space-y-4 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-8"><h1 className="font-editorial text-2xl font-bold">Quản lý công thức</h1><p className="text-sm text-[#66726B]">Vui lòng đăng nhập bằng tài khoản tác giả hoặc quản trị viên.</p><Link href="/auth/login" className="inline-block rounded-lg bg-[#063C2F] px-4 py-2 text-sm font-semibold text-white">Đăng nhập</Link></div>;
  const filtered = recipesQuery.data?.items ?? [];

  return <div className="mx-auto max-w-7xl space-y-7 pb-12">
    <div className="flex flex-wrap items-end justify-between gap-4 border-b border-[#E5DECE] pb-6"><div><p className="text-[11px] font-semibold uppercase tracking-[0.2em] text-[#A8894D]">Bàn làm việc biên tập</p><h1 className="mt-1 font-editorial text-3xl font-bold text-[#1C2421]">Quản Lý Công Thức</h1><p className="mt-2 text-sm text-[#66726B]">Theo dõi và biên tập các công thức của bạn.</p></div><Link href="/dashboard/recipes/new" className="inline-flex items-center gap-2 rounded-lg bg-[#C6A15B] px-4 py-2.5 text-xs font-semibold text-[#022C24] hover:bg-[#B8934E]"><PlusCircle className="h-4 w-4" />Thêm công thức mới</Link></div>
    {notice && <p role="status" className="rounded-xl border border-[#C6A15B]/40 bg-[#FBF8F1] p-4 text-sm text-[#063C2F]">{notice}</p>}
    <div className="flex flex-col items-stretch gap-3 rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between"><label className="relative block w-full sm:w-80"><Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#A8894D]" /><input value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder="Tìm tên công thức..." aria-label="Tìm công thức quản lý" className="w-full rounded-lg border border-[#E5DECE] bg-[#F5F0E6] py-2 pl-9 pr-3 text-xs outline-none focus:border-[#C6A15B]" /></label><label className="flex items-center gap-2 text-xs text-[#66726B]">Trạng thái:<select value={status} onChange={event => { setStatus(event.target.value); setPage(1); }} className="rounded-lg border border-[#E5DECE] bg-[#FBF8F1] px-3 py-2 text-xs text-[#1C2421]"><option value="">Tất cả</option><option value="Published">Đã xuất bản</option><option value="Draft">Bản nháp</option><option value="Archived">Đã lưu trữ</option></select></label></div>
    {recipesQuery.isError && <p role="alert" className="rounded-xl border border-[#B9674A]/30 bg-[#B9674A]/10 p-4 text-sm text-[#8D3C25]">Không thể tải công thức. Vui lòng kiểm tra backend và thử lại.</p>}
    <div className="overflow-hidden rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] shadow-sm"><div className="overflow-x-auto"><table className="w-full min-w-[850px] text-left text-xs"><thead className="border-b border-[#E5DECE] bg-[#F5F0E6] text-[10px] uppercase tracking-wider text-[#66726B]"><tr><th className="px-4 py-4">Tên công thức</th><th className="px-3 py-4">Danh mục</th><th className="px-3 py-4">Độ khó</th><th className="px-3 py-4">Trạng thái</th><th className="px-3 py-4">Cập nhật</th><th className="px-4 py-4 text-right">Thao tác</th></tr></thead><tbody className="divide-y divide-[#E5DECE]">{filtered.map(recipe => <RecipeRow key={recipe.id} recipe={recipe} busy={mutation.isPending} confirmId={confirmId} setConfirmId={setConfirmId} act={(id, action) => { setNotice(""); mutation.mutate({ id, action }); }} />)}</tbody></table></div>{recipesQuery.isPending ? <p className="p-8 text-center text-sm text-[#66726B]">Đang tải công thức...</p> : filtered.length === 0 && !recipesQuery.isError ? <div className="space-y-2 p-10 text-center"><UtensilsCrossed className="mx-auto h-9 w-9 text-[#C6A15B]" /><p className="font-editorial text-base font-bold">Chưa có công thức phù hợp</p><Link href="/dashboard/recipes/new" className="inline-flex items-center gap-1 text-xs font-semibold text-[#063C2F]">Tạo công thức <ArrowRight className="h-3.5 w-3.5" /></Link></div> : null}</div>
    {recipesQuery.data && recipesQuery.data.totalPages > 1 && <nav aria-label="Trang quản lý công thức" className="flex items-center justify-center gap-3 text-xs"><button type="button" disabled={!recipesQuery.data.hasPreviousPage} onClick={() => setPage(page - 1)} className="rounded-lg border border-[#E5DECE] px-3 py-2 disabled:opacity-40">Trước</button><span>Trang {page} / {recipesQuery.data.totalPages}</span><button type="button" disabled={!recipesQuery.data.hasNextPage} onClick={() => setPage(page + 1)} className="rounded-lg border border-[#E5DECE] px-3 py-2 disabled:opacity-40">Sau</button></nav>}
  </div>;
}

function RecipeRow({ recipe, busy, confirmId, setConfirmId, act }: {
  recipe: RecipeSummary; busy: boolean; confirmId: string | null; setConfirmId: (id: string | null) => void;
  act: (id: string, action: "publish" | "unpublish" | "archive" | "unarchive" | "delete") => void;
}) {
  const image = recipeImageUrl(recipe.imageUrl);
  return <tr className="hover:bg-[#F5F0E6]/70"><td className="px-4 py-3"><div className="flex items-center gap-3">{image ? <img src={image} alt="" className="h-11 w-11 shrink-0 rounded-lg object-cover" /> : <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-[#E8DDC8] text-[#A8894D]"><UtensilsCrossed className="h-5 w-5" /></span>}<div><Link href={`/recipes/${encodeURIComponent(recipe.slug)}`} className="font-editorial text-sm font-bold text-[#1C2421] hover:text-[#063C2F]">{recipe.title}</Link><p className="mt-1 text-[11px] text-[#66726B]">{recipe.prepTimeMinutes + recipe.cookingTimeMinutes} phút · {recipe.servings} phần</p></div></div></td><td className="px-3 py-3 text-[#4B5750]">{recipe.categoryName}</td><td className="px-3 py-3 text-[#4B5750]">{difficultyLabels[recipe.difficulty] ?? recipe.difficulty}</td><td className="px-3 py-3"><span className={`rounded-full px-2.5 py-1 text-[10px] font-semibold ${recipe.status === "Published" ? "bg-[#E2EFE6] text-[#13613D]" : recipe.status === "Draft" ? "bg-[#F6EBCF] text-[#896318]" : "bg-[#E8E5DF] text-[#66726B]"}`}>{statusLabels[recipe.status] ?? recipe.status}</span></td><td className="px-3 py-3 text-[#66726B]">{formatRecipeDate(recipe.updatedAt ?? recipe.createdAt)}</td><td className="px-4 py-3"><div className="flex items-center justify-end gap-1.5">{recipe.status !== "Archived" && <button type="button" disabled={busy} onClick={() => act(recipe.id, recipe.status === "Published" ? "unpublish" : "publish")} title={recipe.status === "Published" ? "Hủy xuất bản" : "Xuất bản"} aria-label={recipe.status === "Published" ? "Hủy xuất bản" : "Xuất bản"} className="rounded-lg p-2 text-[#063C2F] hover:bg-[#E5DECE] disabled:opacity-40">{recipe.status === "Published" ? <FileText className="h-4 w-4" /> : <Send className="h-4 w-4" />}</button>}{recipe.status === "Archived" ? <button type="button" disabled={busy} onClick={() => act(recipe.id, "unarchive")} title="Khôi phục bản nháp" aria-label="Khôi phục bản nháp" className="rounded-lg p-2 text-[#66726B] hover:bg-[#E5DECE] disabled:opacity-40"><ArchiveRestore className="h-4 w-4" /></button> : <button type="button" disabled={busy} onClick={() => act(recipe.id, "archive")} title="Lưu trữ" aria-label="Lưu trữ" className="rounded-lg p-2 text-[#66726B] hover:bg-[#E5DECE] disabled:opacity-40"><Archive className="h-4 w-4" /></button>}<Link href={`/recipes/${encodeURIComponent(recipe.slug)}`} title="Xem chi tiết" aria-label="Xem chi tiết" className="rounded-lg p-2 text-[#66726B] hover:bg-[#E5DECE]"><ExternalLink className="h-4 w-4" /></Link><Link href={`/dashboard/recipes/${recipe.id}/edit?slug=${encodeURIComponent(recipe.slug)}`} title="Chỉnh sửa" aria-label="Chỉnh sửa" className="rounded-lg p-2 text-[#66726B] hover:bg-[#E5DECE]"><Edit3 className="h-4 w-4" /></Link><button type="button" disabled={busy} onClick={() => setConfirmId(recipe.id)} title="Xóa" aria-label="Xóa" className="rounded-lg p-2 text-[#B9674A] hover:bg-[#B9674A]/10 disabled:opacity-40"><Trash2 className="h-4 w-4" /></button></div>{confirmId === recipe.id && <div className="mt-2 rounded-lg border border-[#B9674A]/30 bg-[#FFF7F4] p-2 text-right"><p className="mb-2 text-[11px] text-[#8D3C25]">Xóa công thức này? Dữ liệu và ảnh vẫn được lưu để có thể khôi phục.</p><button type="button" onClick={() => setConfirmId(null)} className="mr-2 text-[11px]">Hủy</button><button type="button" disabled={busy} onClick={() => act(recipe.id, "delete")} className="rounded bg-[#B9674A] px-2 py-1 text-[11px] text-white disabled:opacity-40">Xác nhận xóa</button></div>}</td></tr>;
}
