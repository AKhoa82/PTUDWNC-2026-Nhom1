"use client";

import RecipeCard from "../../../components/recipes/RecipeCard";
import { recipeRequest, type PagedRecipes } from "../../../lib/recipes";
import { readAuthSession } from "../../../lib/auth-session";
import { useEffect, useState } from "react";

export default function ClientRecipeList({ categoryId, initialRecipes }: { categoryId: string, initialRecipes: PagedRecipes }) {
  const [recipes, setRecipes] = useState<PagedRecipes>(initialRecipes);
  const [page, setPage] = useState(1);
  const [error, setError] = useState("");

  useEffect(() => {
    // Quét token từ mọi nguồn khi trình duyệt load xong component
    const session = readAuthSession();
    const token = (session as any)?.accessToken 
               || (session as any)?.backendAuth?.accessToken 
               || (typeof window !== "undefined" ? localStorage.getItem("accessToken") : "");

    if (page === 1 && !token) {
      setRecipes(initialRecipes);
      return;
    }

    let active = true;
    const query = new URLSearchParams({ categoryId, categoryView: "true", page: String(page), pageSize: "12", sort: "-createdAt" });
    recipeRequest<PagedRecipes>(`/v1/recipes?${query}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    })
      .then(data => {
        if (active) {
          setRecipes(data);
          setError("");
        }
      })
      .catch(() => {
        if (active) setError("Không thể tải trang công thức. Vui lòng thử lại.");
      });
    return () => { active = false; };
  }, [categoryId, initialRecipes, page]);

  return (
    <>
      {recipes.items.length > 0 ? (
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {recipes.items.map(recipe => <RecipeCard key={recipe.id} recipe={recipe} />)}
        </div>
      ) : (
        <p className="rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-8 text-sm text-[#66726B]">
          Danh mục này chưa có công thức nào.
        </p>
      )}
      {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
      {recipes.totalPages > 1 && (
        <nav aria-label="Phân trang công thức" className="flex items-center gap-4 text-sm">
          <button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)} className="font-semibold text-[#063C2F] disabled:opacity-40">Trang trước</button>
          <span>Trang {page} / {recipes.totalPages}</span>
          <button type="button" disabled={!recipes.hasNextPage} onClick={() => setPage(page + 1)} className="font-semibold text-[#063C2F] disabled:opacity-40">Trang sau</button>
        </nav>
      )}
    </>
  );
}
