import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = { title: "Tìm công thức | CulinaryBlog" };

type SearchParams = Record<string, string | string[] | undefined>;
type Recipe = {
  id: string;
  title: string;
  description: string | null;
  categoryName: string;
  cookingTimeMinutes: number;
};
type PagedResult = {
  items: Recipe[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
};
type Category = { id: string; name: string };

const apiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");
const fieldClass = "w-full rounded-lg border border-[#d8d0c0] bg-white p-3 text-[#022c24]";
const linkClass = "rounded-lg border border-[#d8d0c0] px-4 py-2 hover:bg-[#e9e2d3] focus-visible:outline-2";

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`, { cache: "no-store", signal: AbortSignal.timeout(10000) });
  if (!response.ok) throw new Error("Không thể tải dữ liệu.");
  return response.json() as Promise<T>;
}

export default async function RecipesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const values = await searchParams;
  const value = (key: string, fallback = "") => {
    const raw = values[key];
    return (Array.isArray(raw) ? raw[0] : raw) ?? fallback;
  };
  const page = Number(value("page", "1"));
  const pageSize = Number(value("pageSize", "12"));
  const validPage = Number.isInteger(page) && page >= 1 && page <= 2147483647;
  const validSize = Number.isInteger(pageSize) && pageSize >= 1 && pageSize <= 50;
  const query = new URLSearchParams();
  for (const key of ["keyword", "categoryId", "difficulty", "maxCookTime", "sort"]) {
    const input = value(key).trim();
    if (input) query.set(key, input);
  }
  query.set("page", String(page));
  query.set("pageSize", String(pageSize));
  const pageHref = (target: number) => {
    const next = new URLSearchParams(query);
    next.set("page", String(target));
    return `/recipes?${next}`;
  };

  let result: PagedResult | null = null;
  let categories: Category[] = [];
  let error = "";
  if (!validPage || !validSize) {
    error = "Trang phải là số nguyên từ 1 đến 2147483647; số kết quả mỗi trang từ 1 đến 50.";
  } else {
    const [recipesResponse, categoriesResponse] = await Promise.allSettled([
      getJson<PagedResult>(`/v1/recipes?${query}`),
      getJson<Category[]>("/v1/categories"),
    ]);
    if (recipesResponse.status === "fulfilled") result = recipesResponse.value;
    else error = "Không thể tải công thức. Vui lòng kiểm tra bộ lọc hoặc thử lại sau.";
    if (categoriesResponse.status === "fulfilled") categories = categoriesResponse.value;
  }

  return (
    <main className="mx-auto max-w-6xl space-y-8 px-5 py-10 text-[#022c24]">
      <header className="space-y-2">
        <Link href="/" className="text-sm underline">CulinaryBlog</Link>
        <h1 className="text-3xl font-bold">Khám phá công thức</h1>
        <p>Tìm món ăn yêu thích và khám phá những ý tưởng cho bữa ăn tiếp theo.</p>
      </header>

      <form action="/recipes" method="get" className="grid gap-4 rounded-xl bg-[#eee7da] p-5 sm:grid-cols-2 lg:grid-cols-3">
        <label className="space-y-1">Từ khóa<input name="keyword" defaultValue={value("keyword")} className={fieldClass} placeholder="Tên hoặc mô tả món ăn" /></label>
        <label className="space-y-1">Danh mục
          <select name="categoryId" defaultValue={value("categoryId")} className={fieldClass}>
            <option value="">Tất cả danh mục</option>
            {value("categoryId") && !categories.some(category => category.id === value("categoryId")) && <option value={value("categoryId")}>Danh mục đã chọn</option>}
            {categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
          </select>
        </label>
        <label className="space-y-1">Độ khó
          <select name="difficulty" defaultValue={value("difficulty")} className={fieldClass}>
            <option value="">Tất cả mức độ</option><option value="Easy">Dễ</option><option value="Medium">Trung bình</option><option value="Hard">Khó</option><option value="Expert">Chuyên gia</option>
          </select>
        </label>
        <label className="space-y-1">Thời gian nấu tối đa (phút)<input name="maxCookTime" type="number" min="0" max="2147483647" defaultValue={value("maxCookTime")} className={fieldClass} /></label>
        <label className="space-y-1">Sắp xếp
          <select name="sort" defaultValue={value("sort", "-createdAt")} className={fieldClass}>
            <option value="-createdAt">Mới nhất</option><option value="createdAt">Cũ nhất</option><option value="title">Tên A–Z</option><option value="-title">Tên Z–A</option><option value="cookTime">Nấu nhanh nhất</option><option value="-cookTime">Nấu lâu nhất</option><option value="-publishedAt">Xuất bản mới nhất</option><option value="publishedAt">Xuất bản cũ nhất</option>
          </select>
        </label>
        <label className="space-y-1">Kết quả mỗi trang<input name="pageSize" type="number" min="1" max="50" required defaultValue={validSize ? pageSize : 12} className={fieldClass} /></label>
        <div className="flex items-center gap-4 sm:col-span-2 lg:col-span-3">
          <button type="submit" className="rounded-lg bg-[#022c24] px-5 py-3 text-white">Tìm kiếm</button>
          <Link href="/recipes" className="underline">Xóa bộ lọc</Link>
        </div>
      </form>

      {error && <p role="alert" className="rounded-lg bg-red-50 p-4 text-red-800">{error}</p>}
      {result && <>
        <p role="status">{result.items.length > 0
          ? `Hiển thị ${(result.page - 1) * result.pageSize + 1}–${(result.page - 1) * result.pageSize + result.items.length} trong ${result.totalCount} công thức`
          : `Có ${result.totalCount} công thức phù hợp`}</p>
        {result.items.length === 0 ? (
          <div className="space-y-3 rounded-xl border border-[#d8d0c0] p-6">
            <p>{result.totalCount === 0 ? "Không tìm thấy công thức. Hãy thử từ khóa hoặc bộ lọc khác." : "Trang này không có kết quả."}</p>
            {result.page > 1 && <Link href={pageHref(1)} className="inline-block underline">Về trang đầu</Link>}
          </div>
        ) : <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {result.items.map(recipe => <article key={recipe.id} className="space-y-3 rounded-xl border border-[#d8d0c0] bg-white p-5">
            <p className="text-sm text-[#617267]">{recipe.categoryName} · {recipe.cookingTimeMinutes} phút nấu</p>
            <h2 className="text-xl font-semibold">{recipe.title}</h2>
            <p className="text-sm leading-relaxed">{recipe.description}</p>
          </article>)}
        </div>}
        {result.totalPages > 0 && <nav aria-label="Phân trang kết quả" className="flex flex-wrap items-center justify-center gap-2">
          {result.hasPreviousPage ? <><Link href={pageHref(1)} className={linkClass}>Đầu</Link><Link href={pageHref(result.page - 1)} rel="prev" className={linkClass}>Trước</Link></> : <span aria-disabled="true" className="px-4 py-2 opacity-50">Trước</span>}
          {Array.from({ length: Math.min(5, result.totalPages) }, (_, index) => Math.max(1, Math.min(result.page - 2, result.totalPages - 4)) + index).map(number =>
            <Link key={number} href={pageHref(number)} aria-label={`Trang ${number}`} aria-current={number === result.page ? "page" : undefined} className={`${linkClass} ${number === result.page ? "bg-[#022c24] text-white hover:bg-[#064e3b]" : ""}`}>{number}</Link>
          )}
          {result.hasNextPage ? <><Link href={pageHref(result.page + 1)} rel="next" className={linkClass}>Sau</Link><Link href={pageHref(result.totalPages)} className={linkClass}>Cuối</Link></> : <span aria-disabled="true" className="px-4 py-2 opacity-50">Sau</span>}
        </nav>}
      </>}
    </main>
  );
}
