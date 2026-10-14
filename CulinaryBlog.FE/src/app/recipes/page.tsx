import Link from "next/link";
import type { Metadata } from "next";
import { ArrowRight, RotateCcw, Search, UtensilsCrossed } from "lucide-react";
import { RecipeCard } from "../../components/recipes/RecipeCard";
import { recipeRequest, type PagedRecipes, type RecipeCategory } from "../../lib/recipes";

export const metadata: Metadata = { title: "Khám phá công thức | CulinaryBlog" };
export const dynamic = "force-dynamic";

type SearchParams = Record<string, string | string[] | undefined>;
const sortOptions = [
  ["-createdAt", "Mới nhất"], ["createdAt", "Cũ nhất"], ["title", "Tên A–Z"],
  ["-title", "Tên Z–A"], ["cookTime", "Nấu nhanh nhất"], ["-cookTime", "Nấu lâu nhất"],
];
const controlClass = "w-full rounded-lg border border-[#E5DECE] bg-[#FBF8F1] px-3 py-2.5 text-xs text-[#1C2421] outline-none focus:border-[#C6A15B] focus:ring-2 focus:ring-[#C6A15B]/20";
const labelClass = "mb-1.5 block text-[11px] font-semibold uppercase tracking-wider text-[#66726B]";

export default async function RecipesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const values = await searchParams;
  const value = (key: string) => {
    const raw = values[key];
    return (Array.isArray(raw) ? raw[0] : raw) ?? "";
  };
  const pageNumber = Number(value("page") || "1");
  const page = Number.isSafeInteger(pageNumber) && pageNumber > 0 ? pageNumber : 1;
  const sort = sortOptions.find(option => option[0] === value("sort"))?.[0] ?? "-createdAt";
  const filters = new URLSearchParams({ page: String(page), pageSize: "12", sort });
  for (const key of ["keyword", "categoryId", "difficulty", "maxCookTime"]) {
    const text = value(key).trim();
    if (text) filters.set(key, text);
  }
  const pageHref = (nextPage: number) => {
    const next = new URLSearchParams(filters);
    next.set("page", String(nextPage));
    return `/recipes?${next}`;
  };

  const [recipeResult, categoryResult] = await Promise.allSettled([
    recipeRequest<PagedRecipes>(`/v1/recipes?${filters}`, { cache: "no-store", signal: AbortSignal.timeout(10000) }),
    recipeRequest<RecipeCategory[]>("/v1/categories", { cache: "no-store", signal: AbortSignal.timeout(10000) }),
  ]);
  const result = recipeResult.status === "fulfilled" ? recipeResult.value : null;
  const categories = categoryResult.status === "fulfilled" ? categoryResult.value : [];
  const hasFilters = ["keyword", "categoryId", "difficulty", "maxCookTime"].some(key => Boolean(value(key)));

  return <main className="mx-auto w-full max-w-7xl space-y-8 px-4 pb-20 pt-10 sm:px-6 lg:px-8">
    <header className="border-b border-[#E5DECE] pb-6">
      <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.2em] text-[#A8894D]">Tuyển tập ẩm thực</p>
      <h1 className="font-editorial text-3xl font-bold tracking-tight text-[#1C2421] sm:text-4xl">Khám Phá Công Thức Nấu Ăn</h1>
      <p className="mt-2 max-w-2xl text-sm leading-6 text-[#66726B]">Tìm món ăn phù hợp với khẩu vị của bạn, từ món quen thuộc đến những cách nấu mới.</p>
    </header>

    <form action="/recipes" method="get" className="space-y-4 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-5 shadow-sm sm:p-6">
      <label className="block"><span className="sr-only">Tìm công thức</span><span className="relative block"><Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#A8894D]" /><input name="keyword" defaultValue={value("keyword")} placeholder="Tìm theo tên món ăn hoặc mô tả..." className={`${controlClass} pl-10`} /></span></label>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <label><span className={labelClass}>Danh mục</span><select name="categoryId" defaultValue={value("categoryId")} className={controlClass}><option value="">Tất cả danh mục</option>{categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
        <label><span className={labelClass}>Độ khó</span><select name="difficulty" defaultValue={value("difficulty")} className={controlClass}><option value="">Tất cả mức độ</option><option value="Easy">Dễ</option><option value="Medium">Trung bình</option><option value="Hard">Khó</option><option value="Expert">Chuyên gia</option></select></label>
        <label><span className={labelClass}>Thời gian nấu tối đa</span><input name="maxCookTime" type="number" min="0" max="10000" defaultValue={value("maxCookTime")} placeholder="Số phút" className={controlClass} /></label>
        <label><span className={labelClass}>Sắp xếp theo</span><select name="sort" defaultValue={sort} className={controlClass}>{sortOptions.map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      </div>
      <div className="flex flex-wrap items-center gap-3 border-t border-[#E5DECE] pt-4">
        <button type="submit" className="inline-flex items-center gap-2 rounded-lg bg-[#063C2F] px-5 py-2.5 text-xs font-semibold text-white hover:bg-[#022C24]"><Search className="h-4 w-4" />Tìm công thức</button>
        <Link href="/recipes" className="inline-flex items-center gap-2 rounded-lg border border-[#E5DECE] px-4 py-2.5 text-xs font-semibold text-[#063C2F] hover:bg-[#F5F0E6]"><RotateCcw className="h-3.5 w-3.5" />Đặt lại bộ lọc</Link>
      </div>
    </form>

    <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-[#66726B]">
      <p>{result ? <>Tìm thấy <strong className="font-editorial text-sm text-[#1C2421]">{result.totalCount}</strong> công thức phù hợp</> : "Chưa tải được danh sách công thức."}</p>
      {hasFilters && <span className="font-semibold uppercase tracking-wider text-[#063C2F]">Đang áp dụng bộ lọc</span>}
    </div>
    {recipeResult.status === "rejected" && <p role="alert" className="rounded-xl border border-[#B9674A]/30 bg-[#B9674A]/10 p-4 text-sm text-[#8D3C25]">Không thể tải công thức. Vui lòng kiểm tra kết nối backend và thử lại.</p>}
    {result && (result.items.length ? <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">{result.items.map(recipe => <RecipeCard key={recipe.id} recipe={recipe} />)}</div> : <div className="space-y-3 rounded-2xl border border-dashed border-[#E5DECE] bg-[#FBF8F1] p-12 text-center"><UtensilsCrossed className="mx-auto h-10 w-10 text-[#C6A15B]" /><h2 className="font-editorial text-lg font-bold">Không tìm thấy công thức phù hợp</h2><p className="text-xs text-[#66726B]">Hãy thử từ khóa hoặc bộ lọc khác.</p><Link href="/recipes" className="inline-flex items-center gap-1 text-xs font-semibold text-[#063C2F]">Xem tất cả công thức <ArrowRight className="h-3.5 w-3.5" /></Link></div>)}
    {result && result.totalPages > 1 && <nav aria-label="Phân trang công thức" className="flex flex-wrap items-center justify-center gap-2">
      {result.hasPreviousPage && <Link href={pageHref(page - 1)} rel="prev" className="rounded-lg border border-[#E5DECE] px-4 py-2 text-xs font-semibold hover:bg-[#FBF8F1]">Trước</Link>}
      {Array.from({ length: Math.min(5, result.totalPages) }, (_, index) => Math.max(1, Math.min(page - 2, result.totalPages - 4)) + index).map(number => <Link key={number} href={pageHref(number)} aria-current={number === page ? "page" : undefined} className={`rounded-lg border px-3 py-2 text-xs font-semibold ${number === page ? "border-[#063C2F] bg-[#063C2F] text-white" : "border-[#E5DECE] hover:bg-[#FBF8F1]"}`}>{number}</Link>)}
      {result.hasNextPage && <Link href={pageHref(page + 1)} rel="next" className="rounded-lg border border-[#E5DECE] px-4 py-2 text-xs font-semibold hover:bg-[#FBF8F1]">Sau</Link>}
    </nav>}
  </main>;
}
