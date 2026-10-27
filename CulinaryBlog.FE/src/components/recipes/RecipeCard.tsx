import Link from "next/link";
import { ArrowRight, Clock3, UsersRound, UtensilsCrossed } from "lucide-react";
import { difficultyLabels, recipeImageUrl, type RecipeSummary } from "../../lib/recipes";

export function RecipeCard({ recipe }: { recipe: RecipeSummary }) {
  const image = recipeImageUrl(recipe.imageUrl);
  return <article className="group flex flex-col overflow-hidden rounded-xl border border-[#E5DECE] bg-[#FBF8F1] shadow-sm transition-all hover:border-[#C6A15B]/60 hover:shadow-md">
    <Link href={`/recipes/${encodeURIComponent(recipe.slug)}`} className="relative block aspect-[8/5] overflow-hidden bg-[#E7DED0]" aria-label={`Xem ${recipe.title}`}>
      {image ? <img src={image} alt={recipe.title} loading="lazy" className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105" />
        : <span className="flex h-full items-center justify-center bg-[radial-gradient(circle_at_top_left,#e5d6b8,transparent_60%),linear-gradient(135deg,#f5f0e6,#e7ded0)] text-[#A8894D]"><UtensilsCrossed className="h-12 w-12" /></span>}
      <span className="absolute left-3 top-3 rounded-full border border-[#C6A15B]/30 bg-[#022C24]/90 px-2.5 py-1 text-[10px] font-semibold text-[#FBF8F1]">{recipe.categoryName}</span>
    </Link>
    <div className="flex flex-1 flex-col p-5 sm:p-6">
      <span className="mb-2 text-[11px] font-semibold uppercase tracking-wider text-[#A8894D]">{difficultyLabels[recipe.difficulty] ?? recipe.difficulty}</span>
      <h2 className="font-editorial text-lg font-bold leading-snug text-[#1C2421]"><Link href={`/recipes/${encodeURIComponent(recipe.slug)}`} className="hover:text-[#063C2F]">{recipe.title}</Link></h2>
      <p className="mt-2 line-clamp-2 min-h-10 text-xs leading-relaxed text-[#66726B]">{recipe.description || "Khám phá cách chế biến món ăn này trên CulinaryBlog."}</p>
      <div className="mt-auto flex flex-wrap gap-4 border-t border-[#E5DECE] pt-4 text-xs text-[#66726B]">
        <span className="inline-flex items-center gap-1"><Clock3 className="h-3.5 w-3.5 text-[#A8894D]" />{recipe.prepTimeMinutes + recipe.cookingTimeMinutes} phút</span>
        <span className="inline-flex items-center gap-1"><UsersRound className="h-3.5 w-3.5 text-[#A8894D]" />{recipe.servings} phần</span>
      </div>
      <Link href={`/recipes/${encodeURIComponent(recipe.slug)}`} className="mt-4 inline-flex items-center gap-1 text-xs font-semibold text-[#063C2F] hover:text-[#A8894D]">Chi tiết công thức <ArrowRight className="h-3.5 w-3.5" /></Link>
    </div>
  </article>;
}
