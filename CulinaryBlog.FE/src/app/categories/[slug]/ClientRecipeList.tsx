"use client";

import RecipeCard from "../../../components/recipes/RecipeCard";
import type { PagedRecipes } from "../../../lib/recipes";
import { readAuthSession } from "../../../lib/auth-session";
import { useEffect, useState } from "react";
import { Link } from "lucide-react";

export default function ClientRecipeList({ categoryId, initialRecipes }: { categoryId: string, initialRecipes: PagedRecipes }) {
  const [recipes, setRecipes] = useState<PagedRecipes>(initialRecipes);

  useEffect(() => {
    // Quét token từ mọi nguồn khi trình duyệt load xong component
    const session = readAuthSession();
    const token = (session as any)?.accessToken 
               || (session as any)?.backendAuth?.accessToken 
               || (typeof window !== "undefined" ? localStorage.getItem("accessToken") : "");

    // Nếu người dùng có Token, gọi lại API để Backend trả về cả các bài Draft
    if (token) {
      const query = new URLSearchParams({ categoryId, page: "1", pageSize: "12", sort: "-createdAt" });
      fetch(`http://localhost:5018/api/v1/recipes?${query}`, {
        headers: { Authorization: `Bearer ${token}` }
      })
      .then(res => res.ok ? res.json() : initialRecipes)
      .then(data => setRecipes(data))
      .catch(() => setRecipes(initialRecipes));
    }
  }, [categoryId, initialRecipes]);

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
      {recipes.hasNextPage && (
        <Link href={`/recipes?categoryId=${categoryId}`} className="inline-block text-sm font-semibold text-[#063C2F] hover:underline">
          Xem thêm công thức →
        </Link>
      )}
    </>
  );
}