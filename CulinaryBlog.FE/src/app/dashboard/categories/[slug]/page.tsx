import RecipeCard from "@/components/recipes/RecipeCard";
import { notFound } from "next/navigation";
import type { PagedRecipes, RecipeCategory } from "@/lib/recipes";

type CategoryDetail = {
  category: RecipeCategory & { description?: string };
  recipes: PagedRecipes;
};

async function getCategoryDetail(slug: string): Promise<CategoryDetail | null> {
  const res = await fetch(`http://localhost:5018/api/v1/categories/${slug}`, {
    cache: "no-store"
  });
  if (!res.ok) return null;
  return res.json();
}

export default async function CategoryDetailPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const detail = await getCategoryDetail(slug);

  if (!detail) {
    notFound();
  }

  const { category, recipes } = detail;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
      <div className="mb-10 text-center">
        <h1 className="text-4xl font-extrabold text-gray-900">{category.name}</h1>
        {category.description && (
          <p className="mt-4 text-xl text-gray-500">{category.description}</p>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        {recipes.items.length > 0 ? (
          recipes.items.map((recipe) => (
            <RecipeCard key={recipe.id} recipe={recipe} />
          ))
        ) : (
          <p className="col-span-full text-center text-gray-500">Chưa có công thức nào trong danh mục này.</p>
        )}
      </div>
    </div>
  );
}
