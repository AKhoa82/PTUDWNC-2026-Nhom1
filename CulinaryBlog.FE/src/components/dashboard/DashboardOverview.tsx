"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, BookOpen, Clock3, FolderTree, PlusCircle, TrendingUp, UtensilsCrossed } from "lucide-react";
import { Badge } from "../ui/Badge";
import { DashboardStatSkeleton, TableRowSkeleton } from "../ui/Skeleton";
import { AUTH_CHANGED_EVENT, readAuthSession, type AuthSession } from "../../lib/auth-session";

type Recipe = {
  id: string;
  title: string;
  categoryName: string;
  status: string;
  imageUrl: string | null;
  cookingTimeMinutes: number;
  createdAt: string;
  updatedAt: string | null;
};
type PagedRecipes = { items: Recipe[]; totalCount: number };
type Category = { id: string; name: string; recipeCount: number };

const apiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");
const statusLabels: Record<string, string> = { Published: "Đã xuất bản", Draft: "Bản nháp", Archived: "Đã lưu trữ" };

async function getJson<T>(path: string, authenticated = false): Promise<T> {
  const token = authenticated ? localStorage.getItem("accessToken") : null;
  const response = await fetch(`${apiUrl}${path}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    cache: "no-store",
  });
  if (!response.ok) throw new Error("Không thể tải dữ liệu dashboard.");
  return response.json() as Promise<T>;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("vi-VN").format(new Date(value));
}

export function DashboardOverview() {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [authReady, setAuthReady] = useState(false);

  useEffect(() => {
    const syncSession = () => {
      setSession(readAuthSession());
      setAuthReady(true);
    };
    syncSession();
    window.addEventListener("storage", syncSession);
    window.addEventListener(AUTH_CHANGED_EVENT, syncSession);
    return () => {
      window.removeEventListener("storage", syncSession);
      window.removeEventListener(AUTH_CHANGED_EVENT, syncSession);
    };
  }, []);

  const recipesQuery = useQuery({
    queryKey: ["dashboard", "recipes", session?.id, session?.role],
    queryFn: () => getJson<PagedRecipes>(`/v1/recipes?page=1&pageSize=8&sort=-createdAt${session?.role === "author" ? `&authorId=${encodeURIComponent(session.id)}` : ""}`, true),
    enabled: authReady && (session?.role === "author" || session?.role === "admin"),
    staleTime: 60_000,
  });
  const categoriesQuery = useQuery({
    queryKey: ["dashboard", "categories"],
    queryFn: () => getJson<Category[]>("/v1/categories"),
    staleTime: 60_000,
  });

  const recipes = recipesQuery.data?.items ?? [];
  const categories = categoriesQuery.data ?? [];
  const publishedCount = recipes.filter(recipe => recipe.status === "Published").length;
  const averageCookTime = recipes.length
    ? Math.round(recipes.reduce((sum, recipe) => sum + recipe.cookingTimeMinutes, 0) / recipes.length)
    : 0;
  const categoryRecipeTotal = categories.reduce((sum, category) => sum + category.recipeCount, 0);
  const popularCategories = [...categories].sort((a, b) => b.recipeCount - a.recipeCount).slice(0, 5);
  const stats = [
    { label: session?.role === "admin" ? "Tổng công thức hệ thống" : "Công thức của tôi", value: recipesQuery.data?.totalCount ?? 0, note: session?.role === "admin" ? "Toàn hệ thống" : "Do bạn tạo", icon: UtensilsCrossed, color: "text-[#C6A15B]" },
    { label: "Đã xuất bản", value: publishedCount, note: "Trong 8 công thức gần nhất", icon: BookOpen, color: "text-[#063C2F]" },
    { label: "Danh mục món ăn", value: categories.length, note: "Danh mục hiện có", icon: FolderTree, color: "text-[#B9674A]" },
    { label: "Thời gian nấu TB", value: `${averageCookTime} phút`, note: "Trong 8 công thức gần nhất", icon: Clock3, color: "text-[#C6A15B]" },
  ];

  return (
    <div className="mx-auto max-w-7xl space-y-8">
      <section className="relative flex flex-col justify-between gap-5 overflow-hidden rounded-2xl border border-[#063C2F] bg-[#022C24] p-6 text-[#FBF8F1] shadow-sm sm:flex-row sm:items-center sm:p-8">
        <div className="absolute inset-0 opacity-10 bg-[radial-gradient(#C6A15B_1px,transparent_1px)] [background-size:16px_16px]" />
        <div className="relative z-10">
          <span className="rounded-full border border-[#C6A15B]/30 bg-[#063C2F] px-3 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-[#C6A15B]">Bàn làm việc biên tập</span>
          <h1 className="mt-4 font-editorial text-2xl font-bold sm:text-3xl">Chào mừng trở lại, {session?.name ?? "bạn"}!</h1>
          <p className="mt-2 max-w-xl text-sm leading-6 text-[#F5F0E6]/80">Theo dõi công thức mới và khám phá những danh mục được cộng đồng quan tâm.</p>
        </div>
        <Link href="/dashboard/recipes/new" className="relative z-10 inline-flex shrink-0 items-center gap-2 rounded-lg bg-[#C6A15B] px-4 py-2.5 text-sm font-semibold text-[#022C24] hover:bg-[#B8934E]"><PlusCircle className="h-4 w-4" />Biên soạn món mới</Link>
      </section>

      <section aria-label="Thống kê tổng quan" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {recipesQuery.isPending || categoriesQuery.isPending
          ? Array.from({ length: 4 }, (_, index) => <DashboardStatSkeleton key={index} />)
          : stats.map(stat => {
              const Icon = stat.icon;
              return <div key={stat.label} className="space-y-3 rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-5 shadow-sm">
                <div className="flex items-center justify-between"><span className="text-xs font-semibold text-[#66726B]">{stat.label}</span><span className="rounded-lg border border-[#E5DECE] bg-[#F5F0E6] p-2"><Icon className={`h-4 w-4 ${stat.color}`} /></span></div>
                <p className="font-editorial text-3xl font-bold text-[#1C2421]">{stat.value}</p>
                <p className="flex items-center gap-1 text-[11px] font-medium text-[#063C2F]"><TrendingUp className="h-3.5 w-3.5 text-[#C6A15B]" />{stat.note}</p>
              </div>;
            })}
      </section>

      {(recipesQuery.isError || categoriesQuery.isError) && <p role="alert" className="rounded-xl border border-[#B9674A]/30 bg-[#B9674A]/10 p-4 text-sm text-[#8D3C25]">Không thể tải một số dữ liệu. Vui lòng kiểm tra kết nối và thử lại.</p>}

      <div className="grid gap-6 lg:grid-cols-3">
        <section className="space-y-4 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm lg:col-span-2">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div><h2 className="font-editorial text-lg font-bold text-[#1C2421]">{session?.role === "admin" ? "Công thức mới toàn hệ thống" : "Công thức mới của tôi"}</h2><p className="mt-1 text-xs text-[#66726B]">Những công thức được tạo gần nhất</p></div>
            <Link href="/dashboard/recipes" className="inline-flex items-center gap-1 text-xs font-semibold text-[#063C2F] hover:text-[#A8894D]">Xem tất cả <ArrowRight className="h-3.5 w-3.5" /></Link>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[520px] text-left text-xs">
              <thead className="border-b border-[#E5DECE] text-[10px] uppercase tracking-wider text-[#66726B]"><tr><th className="pb-3 font-semibold">Công thức</th><th className="pb-3 font-semibold">Danh mục</th><th className="pb-3 font-semibold">Trạng thái</th><th className="pb-3 text-right font-semibold">Thời gian nấu</th></tr></thead>
              <tbody className="divide-y divide-[#E5DECE]">
                {recipesQuery.isPending
                  ? Array.from({ length: 4 }, (_, index) => <TableRowSkeleton key={index} columns={4} />)
                  : recipes.slice(0, 4).map(recipe => <tr key={recipe.id}>
                      <td className="py-3 pr-3"><div className="flex items-center gap-3">
                        {recipe.imageUrl ? <img src={recipe.imageUrl} alt="" className="h-10 w-10 shrink-0 rounded-lg object-cover ring-1 ring-[#E5DECE]" /> : <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[#F5F0E6] text-[#A8894D]"><UtensilsCrossed className="h-5 w-5" /></span>}
                        <span><strong className="line-clamp-1 font-editorial text-sm text-[#1C2421]">{recipe.title}</strong><small className="mt-0.5 block text-[11px] text-[#66726B]">Cập nhật: {formatDate(recipe.updatedAt ?? recipe.createdAt)}</small></span>
                      </div></td>
                      <td className="px-2 py-3 text-[#66726B]">{recipe.categoryName}</td>
                      <td className="px-2 py-3"><Badge variant={recipe.status === "Published" ? "success" : "warning"} size="sm">{statusLabels[recipe.status] ?? recipe.status}</Badge></td>
                      <td className="py-3 pl-2 text-right font-semibold text-[#1C2421]">{recipe.cookingTimeMinutes} phút</td>
                    </tr>)}
              </tbody>
            </table>
            {!recipesQuery.isPending && recipes.length === 0 && !recipesQuery.isError && <p className="py-7 text-center text-sm text-[#66726B]">Chưa có công thức để hiển thị.</p>}
          </div>
        </section>

        <section className="space-y-4 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm">
          <div><h2 className="font-editorial text-lg font-bold text-[#1C2421]">Phân bố danh mục</h2><p className="mt-1 text-xs text-[#66726B]">Tỷ lệ công thức theo danh mục</p></div>
          <div className="space-y-4 pt-2">
            {categoriesQuery.isPending
              ? Array.from({ length: 5 }, (_, index) => <div key={index} className="h-9 animate-pulse rounded bg-[#E8E2D5]" />)
              : popularCategories.map(category => {
                  const percentage = categoryRecipeTotal > 0 ? Math.round(category.recipeCount / categoryRecipeTotal * 100) : 0;
                  return <div key={category.id} className="space-y-1.5"><div className="flex items-center justify-between gap-2 text-xs font-medium text-[#1C2421]"><span>{category.name}</span><span className="font-semibold text-[#063C2F]">{percentage}%</span></div><div className="h-1.5 overflow-hidden rounded-full bg-[#E5DECE]"><div className="h-full rounded-full bg-[#063C2F]" style={{ width: `${percentage}%` }} /></div></div>;
                })}
            {!categoriesQuery.isPending && popularCategories.length === 0 && !categoriesQuery.isError && <p className="text-sm text-[#66726B]">Chưa có danh mục để hiển thị.</p>}
          </div>
          <div className="border-t border-[#E5DECE] pt-4"><Link href="/categories" className="block rounded-lg border border-[#C6A15B]/50 bg-[#FBF8F1] px-4 py-2 text-center text-sm font-semibold text-[#063C2F] hover:bg-[#F5F0E6]">Xem danh mục</Link></div>
        </section>
      </div>
    </div>
  );
}
