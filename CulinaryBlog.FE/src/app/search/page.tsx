import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = { title: "Tìm kiếm toàn văn | CulinaryBlog" };
type Params = Record<string, string | string[] | undefined>;
type Result = {
  items: { id: string; title: string; description: string | null; categoryName: string; cookingTimeMinutes: number; relevanceScore: number }[];
  totalCount: number; totalPages: number; page: number; hasNextPage: boolean; hasPreviousPage: boolean; message?: string;
};
const apiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");
const inputClass = "rounded-lg border border-[#d8d0c0] bg-white p-3";

export default async function SearchPage({ searchParams }: { searchParams: Promise<Params> }) {
  const params = await searchParams;
  const value = (key: string, fallback = "") => {
    const raw = params[key];
    return (Array.isArray(raw) ? raw[0] : raw) ?? fallback;
  };
  const q = value("q").trim();
  const page = Number(value("page", "1"));
  const pageSize = Number(value("pageSize", "12"));
  const validSize = Number.isInteger(pageSize) && pageSize >= 1 && pageSize <= 50;
  let error = "";
  let result: Result | null = null;
  if (params.q !== undefined) {
    if (q.length < 2 || !/[\p{L}\p{N}]/u.test(q)) error = "Nhập từ khóa có ít nhất 2 ký tự và chứa chữ hoặc số.";
    else if (!Number.isInteger(page) || page < 1 || page > 2147483647 || !validSize)
      error = "Trang phải là số nguyên từ 1 đến 2147483647; số kết quả mỗi trang từ 1 đến 50.";
    else {
      try {
        const query = new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) });
        const response = await fetch(`${apiUrl}/v1/recipes/search?${query}`, { cache: "no-store", signal: AbortSignal.timeout(10000) });
        if (!response.ok) throw new Error(String(response.status));
        result = await response.json() as Result;
      } catch {
        error = "Không thể tìm kiếm lúc này. Vui lòng thử lại sau.";
      }
    }
  }
  const href = (target: number) => `/search?${new URLSearchParams({ q, page: String(target), pageSize: String(pageSize) })}`;
  return <main className="mx-auto max-w-6xl space-y-8 px-5 py-10 text-[#022c24]">
    <header className="space-y-2">
      <Link href="/recipes" className="underline">Khám phá công thức</Link>
      <h1 className="text-3xl font-bold">Tìm kiếm công thức</h1>
      <p>Tìm trong tên và mô tả, có dấu hoặc không dấu. Kết quả liên quan nhất hiển thị trước.</p>
    </header>
    <form action="/search" method="get" className="flex flex-wrap items-end gap-4 rounded-xl bg-[#eee7da] p-5">
      <label className="flex flex-1 flex-col gap-1">Từ khóa
        <input key={q} name="q" defaultValue={q} minLength={2} required placeholder="Ví dụ: pho bo" className={inputClass} />
      </label>
      <label className="flex flex-col gap-1">Kết quả mỗi trang
        <input name="pageSize" type="number" min={1} max={50} required defaultValue={validSize ? pageSize : 12} className={inputClass} />
      </label>
      <button className="rounded-lg bg-[#022c24] px-5 py-3 text-white" type="submit">Tìm kiếm</button>
    </form>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-4 text-red-800">{error}</p>}
    {result && <>
      <p role="status">Có {result.totalCount} công thức phù hợp với “{q}”.</p>
      {result.items.length === 0 ? <div className="space-y-3">
        <p>{result.message ?? "Trang này không có kết quả."}</p>
        {page > 1 && <Link href={href(1)} className="underline">Về trang đầu</Link>}
      </div> : <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {result.items.map(recipe => <article key={recipe.id} className="space-y-3 rounded-xl border border-[#d8d0c0] p-5">
          <p className="text-sm">{recipe.categoryName} · {recipe.cookingTimeMinutes} phút nấu</p>
          <h2 className="text-xl font-semibold">{recipe.title}</h2>
          <p>{recipe.description}</p>
        </article>)}
      </div>}
      {result.totalPages > 0 && <nav aria-label="Phân trang kết quả" className="flex gap-5">
        {result.hasPreviousPage && <Link rel="prev" href={href(Math.min(page - 1, result.totalPages))} className="underline">Trước</Link>}
        <span>Trang {result.page} / {result.totalPages}</span>
        {result.hasNextPage && <Link rel="next" href={href(page + 1)} className="underline">Sau</Link>}
      </nav>}
    </>}
  </main>;
}
