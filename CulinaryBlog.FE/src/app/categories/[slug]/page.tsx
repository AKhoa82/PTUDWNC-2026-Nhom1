import Link from "next/link";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { recipeRequest, RecipeRequestError, type PagedRecipes, type RecipeCategory } from "../../../lib/recipes";
// Nhúng Client Component vừa tạo
import ClientRecipeList from "./ClientRecipeList"; 

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

  // Load dữ liệu public ban đầu (SSR) để SEO đọc được
  const query = new URLSearchParams({ categoryId: category.id, page: "1", pageSize: "12", sort: "-createdAt" });
  const publicRecipes = await recipeRequest<PagedRecipes>(`/v1/recipes?${query}`, { cache: "no-store" });

  return (
    <main className="mx-auto w-full max-w-7xl space-y-8 px-4 pb-20 pt-10 sm:px-6 lg:px-8">
      <header className="border-b border-[#E5DECE] pb-6">
        <Link href="/categories" className="text-sm font-semibold text-[#A8894D] hover:underline">Danh mục</Link>
        <h1 className="mt-2 font-editorial text-3xl font-bold text-[#1C2421] sm:text-4xl">{category.name}</h1>
        <p className="mt-2 text-sm text-[#66726B]">Các công thức thuộc danh mục này.</p>
      </header>
      
      {/* Giao phó việc hiển thị và kiểm tra Token cho Client Component */}
      <ClientRecipeList categoryId={category.id} initialRecipes={publicRecipes} />
    </main>
  );
}