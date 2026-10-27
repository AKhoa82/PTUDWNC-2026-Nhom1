import Image from "next/image";
import Link from "next/link";
import type { Metadata } from "next";
import { ArrowRight, BookOpen, ChefHat, Clock3, Flame, Sparkles, UtensilsCrossed, UsersRound } from "lucide-react";

export const metadata: Metadata = {
  title: "Trang chủ | CulinaryBlog",
  description: "Khám phá công thức và danh mục món ăn trên CulinaryBlog.",
};

export const dynamic = "force-dynamic";

type Recipe = {
  id: string;
  slug: string;
  title: string;
  description: string | null;
  imageUrl: string | null;
  categoryId: string;
  categoryName: string;
  prepTimeMinutes: number;
  cookingTimeMinutes: number;
  servings: number;
  difficulty: string;
};
type Category = { id: string; name: string; slug: string; recipeCount: number };
type PagedRecipes = { items: Recipe[]; totalCount: number };

const apiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");
const difficultyLabels: Record<string, string> = { Easy: "Dễ", Medium: "Trung bình", Hard: "Khó", Expert: "Chuyên gia" };

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`, { cache: "no-store", signal: AbortSignal.timeout(5000) });
  if (!response.ok) throw new Error("Không thể tải dữ liệu trang chủ.");
  return response.json() as Promise<T>;
}

function imageSource(value: string | null) {
  if (!value) return null;
  if (/^https?:\/\//i.test(value)) return value;
  if (value.startsWith("/")) return `${new URL(apiUrl).origin}${value}`;
  return `${new URL(apiUrl).origin}/${value}`;
}

function SectionHeading({ eyebrow, title, href, action, icon }: {
  eyebrow: string; title: string; href: string; action: string; icon?: React.ReactNode;
}) {
  return <div className="flex flex-wrap items-end justify-between gap-3 border-b border-[#E5DECE] pb-4">
    <div>
      <p className="mb-1 flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-[0.2em] text-[#A8894D]">{icon}{eyebrow}</p>
      <h2 className="font-editorial text-2xl font-bold tracking-tight text-[#1C2421] sm:text-3xl">{title}</h2>
    </div>
    <Link href={href} className="inline-flex items-center gap-1 text-xs font-semibold text-[#063C2F] hover:text-[#A8894D]">{action}<ArrowRight className="h-3.5 w-3.5" /></Link>
  </div>;
}

function RecipeCard({ recipe }: { recipe: Recipe }) {
  const image = imageSource(recipe.imageUrl);
  return <article className="group flex flex-col overflow-hidden rounded-xl border border-[#E5DECE] bg-[#FBF8F1] shadow-sm transition-all hover:border-[#C6A15B]/60 hover:shadow-md">
    <div className="relative aspect-[8/5] overflow-hidden bg-[#E7DED0]">
      {image ? <img src={image} alt={recipe.title} loading="lazy" className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105" />
        : <div className="flex h-full items-center justify-center bg-[radial-gradient(circle_at_top_left,#e5d6b8,transparent_60%),linear-gradient(135deg,#f5f0e6,#e7ded0)] text-[#A8894D]"><UtensilsCrossed className="h-12 w-12" aria-hidden="true" /></div>}
      <div className="absolute left-3 top-3 flex flex-wrap gap-1.5">
        <span className="rounded-full border border-[#C6A15B]/30 bg-[#022C24]/90 px-2.5 py-1 text-[10px] font-semibold text-[#FBF8F1]">{recipe.categoryName}</span>
        {difficultyLabels[recipe.difficulty] && <span className="rounded-full bg-[#FBF8F1]/90 px-2.5 py-1 text-[10px] font-semibold text-[#063C2F]">{difficultyLabels[recipe.difficulty]}</span>}
      </div>
    </div>
    <div className="flex flex-1 flex-col p-5 sm:p-6">
      <h3 className="font-editorial text-lg font-bold leading-snug text-[#1C2421] group-hover:text-[#063C2F]">{recipe.title}</h3>
      <p className="mt-2 line-clamp-2 min-h-10 text-xs leading-relaxed text-[#66726B]">{recipe.description || "Khám phá cách chế biến món ăn này trên CulinaryBlog."}</p>
      <div className="mt-auto flex flex-wrap items-center gap-x-4 gap-y-2 border-t border-[#E5DECE] pt-4 text-xs text-[#66726B]">
        <span className="inline-flex items-center gap-1"><Clock3 className="h-3.5 w-3.5 text-[#A8894D]" />{recipe.prepTimeMinutes + recipe.cookingTimeMinutes} phút</span>
        <span className="inline-flex items-center gap-1"><UsersRound className="h-3.5 w-3.5 text-[#A8894D]" />{recipe.servings} phần</span>
      </div>
      <Link href={`/recipes/${encodeURIComponent(recipe.slug)}`} className="mt-4 inline-flex items-center gap-1 text-xs font-semibold text-[#063C2F] hover:text-[#A8894D]">Xem công thức <ArrowRight className="h-3.5 w-3.5" /></Link>
    </div>
  </article>;
}

export default async function HomePage() {
  const [recipeResult, categoryResult] = await Promise.allSettled([
    getJson<PagedRecipes>("/v1/recipes?page=1&pageSize=9&sort=-createdAt"),
    getJson<Category[]>("/v1/categories"),
  ]);
  const recipes = recipeResult.status === "fulfilled" ? recipeResult.value.items : [];
  const categories = categoryResult.status === "fulfilled" ? categoryResult.value.slice(0, 6) : [];
  const highlightedRecipes = recipes.slice(0, 3);
  const latestRecipes = recipes.slice(3, 9);

  return <main className="mx-auto w-full max-w-7xl space-y-16 px-4 pb-20 pt-6 sm:px-6 lg:px-8">
    <section className="relative overflow-hidden rounded-2xl border border-[#063C2F] bg-[#022C24] text-[#FBF8F1] shadow-sm">
      <div className="grid min-h-[540px] lg:grid-cols-12">
        <div className="relative z-10 flex flex-col justify-between gap-10 p-8 sm:p-12 lg:col-span-7 lg:p-14">
          <div className="space-y-6">
            <p className="inline-flex items-center gap-2 rounded-full border border-[#C6A15B]/30 bg-[#063C2F]/80 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.2em] text-[#C6A15B]"><Sparkles className="h-3.5 w-3.5" /> Bản sắc ẩm thực Việt</p>
            <div><h1 className="font-editorial text-4xl font-bold leading-[1.1] tracking-tight sm:text-5xl lg:text-6xl">KHƠI NGUỒN<br /><span className="font-normal italic text-[#C6A15B]">CẢM HỨNG</span><br />ẨM THỰC.</h1><div className="mt-6 h-0.5 w-16 bg-[#C6A15B]" /></div>
            <p className="max-w-lg text-sm leading-7 text-[#F5F0E6]/80">Không gian lưu giữ và tôn vinh hương vị ẩm thực Việt. Khám phá công thức mới và chia sẻ cách nấu của bạn với cộng đồng.</p>
          </div>
          <div className="space-y-8">
            <div className="flex flex-wrap gap-3">
              <Link href="/recipes" className="inline-flex items-center gap-2 rounded-lg bg-[#C6A15B] px-4 py-2.5 text-sm font-semibold text-[#022C24] hover:bg-[#B8934E]">Khám phá công thức <ArrowRight className="h-4 w-4" /></Link>
              <Link href="/categories" className="rounded-lg border border-[#C6A15B]/40 px-4 py-2.5 text-sm font-medium text-[#FBF8F1] hover:border-[#C6A15B] hover:bg-[#063C2F]">Bộ sưu tập món ăn</Link>
            </div>
            <div className="grid grid-cols-2 gap-4 border-t border-[#063C2F] pt-6 sm:grid-cols-3">
              <div><strong className="font-editorial text-2xl text-[#C6A15B]">{recipeResult.status === "fulfilled" ? recipeResult.value.totalCount : "—"}</strong><span className="mt-1 block text-[10px] uppercase tracking-wider text-[#F5F0E6]/60">Công thức hiện có</span></div>
              <div className="border-l border-[#063C2F] pl-4"><strong className="font-editorial text-2xl text-[#C6A15B]">{categoryResult.status === "fulfilled" ? categoryResult.value.length : "—"}</strong><span className="mt-1 block text-[10px] uppercase tracking-wider text-[#F5F0E6]/60">Danh mục món ăn</span></div>
              <div className="hidden border-l border-[#063C2F] pl-4 sm:block"><strong className="font-editorial text-2xl text-[#C6A15B]">Việt</strong><span className="mt-1 block text-[10px] uppercase tracking-wider text-[#F5F0E6]/60">Bản sắc ẩm thực</span></div>
            </div>
          </div>
        </div>
        <div className="relative min-h-[300px] overflow-hidden lg:col-span-5 lg:min-h-full">
          <Image src="/images/home-hero.jpg" alt="Món ăn đầy màu sắc trên bàn ăn" fill priority sizes="(min-width: 1024px) 40vw, 100vw" className="object-cover transition-transform duration-1000 hover:scale-105" />
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-[#022C24]/70 via-transparent to-transparent lg:bg-gradient-to-r lg:from-[#022C24] lg:via-[#022C24]/55 lg:to-transparent" />
          <span className="absolute bottom-4 right-4 rounded-md border border-[#C6A15B]/30 bg-[#022C24]/80 px-3.5 py-1.5 text-[11px] text-[#FBF8F1] backdrop-blur-md">Hương vị truyền cảm hứng</span>
        </div>
      </div>
    </section>

    <section className="space-y-6">
      <SectionHeading eyebrow="Bộ sưu tập món" title="Danh Mục Món Ăn Đặc Sắc" href="/categories" action="Tất cả danh mục" />
      {categories.length ? <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">{categories.map((category, index) =>
        <Link key={category.id} href={`/recipes?categoryId=${encodeURIComponent(category.id)}`} className="group flex flex-col overflow-hidden rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-3 text-center transition-all hover:border-[#C6A15B]/60 hover:shadow-sm">
          <span className={`flex aspect-square items-center justify-center rounded-lg ${index % 2 ? "bg-[#E8DDC8]" : "bg-[#E3E9DF]"} text-[#063C2F] transition-colors group-hover:bg-[#D8CAAA]`}><UtensilsCrossed className="h-9 w-9" /></span>
          <strong className="mt-3 line-clamp-1 font-editorial text-xs text-[#1C2421]">{category.name}</strong><span className="mt-1 text-[11px] text-[#8D958F]">{category.recipeCount} công thức</span>
        </Link>)}</div> : <p className="rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-6 text-sm text-[#66726B]">Danh mục sẽ xuất hiện khi dữ liệu sẵn sàng.</p>}
    </section>

    <section className="space-y-6">
      <SectionHeading eyebrow="Gợi ý khám phá" title="Công Thức Đáng Khám Phá" href="/recipes" action="Xem thêm công thức" icon={<Flame className="h-3.5 w-3.5 text-[#B9674A]" />} />
      {highlightedRecipes.length ? <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">{highlightedRecipes.map(recipe => <RecipeCard key={recipe.id} recipe={recipe} />)}</div>
        : <p className="rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-6 text-sm text-[#66726B]">Công thức sẽ xuất hiện khi dữ liệu sẵn sàng.</p>}
    </section>

    <section className="flex flex-col items-start justify-between gap-6 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm sm:p-8 md:flex-row md:items-center">
      <div className="flex items-start gap-4"><span className="shrink-0 rounded-xl bg-[#022C24] p-3.5 text-[#C6A15B]"><ChefHat className="h-6 w-6" /></span><div>
        <p className="mb-1 text-[10px] font-semibold uppercase tracking-widest text-[#A8894D]">Sổ tay ẩm thực gia đình</p>
        <h2 className="font-editorial text-lg font-bold text-[#1C2421]">Bạn có công thức gia truyền muốn lưu giữ và chia sẻ?</h2>
        <p className="mt-1 max-w-xl text-sm leading-relaxed text-[#66726B]">Tham gia CulinaryBlog để khám phá món ngon và cùng cộng đồng lưu giữ những hương vị thân quen.</p>
      </div></div>
      <Link href="/auth/register" className="inline-flex shrink-0 items-center gap-2 rounded-lg bg-[#063C2F] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#022C24]">Tham gia cộng đồng <ArrowRight className="h-4 w-4" /></Link>
    </section>

    <section className="space-y-6">
      <SectionHeading eyebrow="Hương vị mới" title="Món Mới Cập Nhật" href="/recipes" action="Tất cả công thức" icon={<BookOpen className="h-3.5 w-3.5" />} />
      {latestRecipes.length ? <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">{latestRecipes.map(recipe => <RecipeCard key={recipe.id} recipe={recipe} />)}</div>
        : <p className="rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-6 text-sm text-[#66726B]">Chưa có thêm công thức mới để hiển thị.</p>}
    </section>
  </main>;
}
