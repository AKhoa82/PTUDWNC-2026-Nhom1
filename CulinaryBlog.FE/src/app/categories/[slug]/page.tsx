import Link from "next/link";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import RecipeCard from "../../../components/recipes/RecipeCard";
import { recipeRequest, RecipeRequestError, type PagedRecipes, type RecipeCategory } from "../../../lib/recipes";

// Bổ sung thư viện Auth của bạn để lấy Token
import { auth } from "../../../auth"; // CHÚ Ý: Sửa lại đường dẫn này trỏ tới file Auth.js thực tế của bạn

type PageProps = { params: Promise<{ slug: string }> };

async function getCategory(slug: string) {
  const categories = await recipeRequest<RecipeCategory[]>("/v1/categories", { cache: "no-store" });
  return categories.find(category => category.slug === slug);
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const category = await getCategory(slug).catch(() => undefined);
  return { title: category ? `${category.name} | CulinaryBlog` : "Danh mục | CulinaryBlog" };
}

export default async function CategoryPage({ params }: PageProps) {
  const { slug } = await params;
  let category: RecipeCategory | undefined;
  try {
    category = await getCategory(slug);
  } catch (error) {
    if (error instanceof RecipeRequestError && error.status === 404) notFound();
    throw error;
  }
  if (!category) notFound();

  // FR-CAT-002: Lấy Token từ NextAuth để truyền vào Header, giúp Author thấy được bài Draft
  const session = await auth();
  const token = (session as any)?.backendAuth?.accessToken;

  const query = new URLSearchParams({ categoryId: category.id, page: "1", pageSize: "12", sort: "-createdAt" });
  
  const recipes = await recipeRequest<PagedRecipes>(`/v1/recipes?${query}`, { 
    cache: "no-store",
    // Chèn Token vào Fetch Header nếu có
    headers: token ? { Authorization: `Bearer ${token}` } : undefined
  });

  return <main className="mx-auto w-full max-w-7xl space-y-8 px-4 pb-20 pt-10 sm:px-6 lg:px-8">
    <header className="border-b border-[#E5DECE] pb-6">
      <Link href="/categories" className="text-sm font-semibold text-[#A8894D] hover:underline">Danh mục</Link>
      <h1 className="mt-2 font-editorial text-3xl font-bold text-[#1C2421] sm:text-4xl">{category.name}</h1>
      <p className="mt-2 text-sm text-[#66726B]">Các công thức {token ? "trong hệ thống" : "đã xuất bản"} thuộc danh mục này.</p>
    </header>
    {recipes.items.length > 0 ? <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
      {recipes.items.map(recipe => <RecipeCard key={recipe.id} recipe={recipe} />)}
    </div> : <p className="rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-8 text-sm text-[#66726B]">Danh mục này chưa có công thức nào.</p>}
    {recipes.hasNextPage && <Link href={`/recipes?categoryId=${category.id}`} className="inline-block text-sm font-semibold text-[#063C2F] hover:underline">Xem thêm công thức →</Link>}
  </main>;
}