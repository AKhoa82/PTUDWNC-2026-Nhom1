"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, CalendarDays, ChefHat, Check, Clock3, Flame, Minus, Plus, UsersRound, UtensilsCrossed } from "lucide-react";
import { AUTH_CHANGED_EVENT, readAuthSession } from "../../lib/auth-session";
import { authHeaders, difficultyLabels, formatRecipeDate, recipeImageUrl, recipeRequest, type RecipeDetail } from "../../lib/recipes";

export function RecipeDetailView({ slug }: { slug: string }) {
  const [sessionReady, setSessionReady] = useState(false);
  const [sessionId, setSessionId] = useState("guest");
  const [servings, setServings] = useState<number | null>(null);
  const [checkedIngredients, setCheckedIngredients] = useState<string[]>([]);
  const [checkedSteps, setCheckedSteps] = useState<string[]>([]);

  useEffect(() => {
    const sync = () => { setSessionId(readAuthSession()?.id ?? "guest"); setSessionReady(true); };
    sync();
    window.addEventListener("storage", sync);
    window.addEventListener(AUTH_CHANGED_EVENT, sync);
    return () => { window.removeEventListener("storage", sync); window.removeEventListener(AUTH_CHANGED_EVENT, sync); };
  }, []);

  const query = useQuery({
    queryKey: ["recipe", slug, sessionId],
    queryFn: () => recipeRequest<RecipeDetail>(`/v1/recipes/${encodeURIComponent(slug)}`, { headers: authHeaders(), cache: "no-store" }),
    enabled: sessionReady,
    staleTime: 60_000,
  });
  const recipe = query.data;
  const portionCount = Math.max(1, servings ?? recipe?.servings ?? 1);
  const scale = recipe?.servings ? portionCount / recipe.servings : 1;
  const image = recipeImageUrl(recipe?.images.find(item => item.isPrimary)?.originalUrl ?? recipe?.imageUrl);
  const toggle = (id: string, values: string[], setValues: (next: string[]) => void) => setValues(values.includes(id) ? values.filter(item => item !== id) : [...values, id]);

  if (query.isPending || !sessionReady) return <main className="mx-auto max-w-5xl space-y-5 px-5 py-10" aria-busy="true"><div className="h-5 w-32 animate-pulse rounded bg-[#E5DECE]" /><div className="h-12 w-2/3 animate-pulse rounded bg-[#E5DECE]" /><div className="aspect-video animate-pulse rounded-2xl bg-[#E5DECE]" /></main>;
  if (query.isError || !recipe) return <main className="mx-auto max-w-5xl space-y-4 px-5 py-16"><Link href="/recipes" className="inline-flex items-center gap-1 text-xs font-semibold text-[#063C2F]"><ArrowLeft className="h-4 w-4" />Quay lại công thức</Link><p role="alert" className="rounded-xl border border-[#B9674A]/30 bg-[#B9674A]/10 p-5 text-sm text-[#8D3C25]">Không thể mở công thức. Công thức có thể chưa được xuất bản hoặc kết nối backend đang gián đoạn.</p></main>;

  return <main className="mx-auto w-full max-w-5xl space-y-9 px-4 pb-20 pt-10 sm:px-6">
    <Link href="/recipes" className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#66726B] hover:text-[#063C2F]"><ArrowLeft className="h-4 w-4 text-[#C6A15B]" />Quay lại tuyển tập công thức</Link>
    <header className="space-y-4">
      <div className="flex flex-wrap items-center gap-2"><Link href={`/recipes?categoryId=${encodeURIComponent(recipe.categoryId)}`} className="rounded-full border border-[#C6A15B]/30 bg-[#022C24] px-3 py-1 text-[11px] font-semibold text-[#FBF8F1]">{recipe.categoryName}</Link><span className="rounded-full border border-[#E5DECE] bg-[#FBF8F1] px-3 py-1 text-[11px] font-semibold text-[#063C2F]">Độ khó: {difficultyLabels[recipe.difficulty] ?? recipe.difficulty}</span></div>
      <h1 className="font-editorial text-3xl font-bold leading-tight text-[#1C2421] sm:text-5xl">{recipe.title}</h1>
      {recipe.description && <p className="max-w-3xl text-sm leading-7 text-[#4B5750] sm:text-base">{recipe.description}</p>}
      <div className="flex flex-wrap items-center gap-4 border-t border-[#E5DECE] pt-4 text-xs text-[#66726B]"><span className="inline-flex items-center gap-2"><ChefHat className="h-4 w-4 text-[#C6A15B]" />{recipe.author?.fullName || recipe.author?.userName || "Tác giả CulinaryBlog"}</span><span className="inline-flex items-center gap-1"><CalendarDays className="h-4 w-4 text-[#A8894D]" />{formatRecipeDate(recipe.createdAt)}</span></div>
    </header>

    <div className="relative aspect-video overflow-hidden rounded-2xl border border-[#E5DECE] bg-[#E8DDC8] shadow-sm">{image ? <img src={image} alt={recipe.title} className="h-full w-full object-cover" /> : <div className="flex h-full items-center justify-center text-[#A8894D]"><UtensilsCrossed className="h-20 w-20" /></div>}</div>
    <div className="grid grid-cols-2 gap-3 rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-4 text-center shadow-sm sm:grid-cols-4">
      {[{ label: "Chuẩn bị", value: `${recipe.prepTimeMinutes} phút`, icon: Clock3 }, { label: "Nấu chín", value: `${recipe.cookingTimeMinutes} phút`, icon: Clock3 }, { label: "Khẩu phần", value: `${portionCount} phần`, icon: UsersRound }, { label: "Độ khó", value: difficultyLabels[recipe.difficulty] ?? recipe.difficulty, icon: Flame }].map(item => <div key={item.label} className="p-2"><p className="flex items-center justify-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-[#66726B]"><item.icon className="h-3.5 w-3.5 text-[#A8894D]" />{item.label}</p><p className="mt-1 font-editorial text-sm font-bold text-[#1C2421]">{item.value}</p></div>)}
    </div>

    <section className="space-y-5 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm sm:p-8">
      <div className="flex flex-wrap items-center justify-between gap-4 border-b border-[#E5DECE] pb-4"><div><h2 className="font-editorial text-xl font-bold">Nguyên Liệu Cần Chuẩn Bị <span className="text-xs font-normal text-[#66726B]">({recipe.ingredients.length} loại)</span></h2><p className="mt-1 text-xs text-[#66726B]">Đánh dấu những nguyên liệu đã chuẩn bị.</p></div><div className="flex items-center gap-2 rounded-lg border border-[#E5DECE] bg-[#F5F0E6] p-1.5 text-xs"><span className="px-1">Khẩu phần:</span><button type="button" aria-label="Giảm khẩu phần" onClick={() => setServings(Math.max(1, portionCount - 1))} className="rounded border border-[#E5DECE] bg-white p-1"><Minus className="h-3 w-3" /></button><strong className="min-w-8 text-center">{portionCount}</strong><button type="button" aria-label="Tăng khẩu phần" onClick={() => setServings(portionCount + 1)} className="rounded border border-[#E5DECE] bg-white p-1"><Plus className="h-3 w-3" /></button></div></div>
      {recipe.ingredients.length ? <div className="grid gap-3 md:grid-cols-2">{recipe.ingredients.map(item => <label key={item.id} className={`flex cursor-pointer items-start gap-3 rounded-xl border border-[#E5DECE] p-3 text-xs ${checkedIngredients.includes(item.id) ? "bg-[#F5F0E6] text-[#8D958F] line-through" : "bg-[#F5F0E6]/70 text-[#1C2421]"}`}><input type="checkbox" checked={checkedIngredients.includes(item.id)} onChange={() => toggle(item.id, checkedIngredients, setCheckedIngredients)} className="mt-0.5 accent-[#063C2F]" /><span><strong className="font-editorial">{item.quantity == null ? "" : `${Number((item.quantity * scale).toFixed(2))} `}{item.unit || ""}</strong> {item.name}{item.notes && <small className="mt-1 block text-[#66726B]">{item.notes}</small>}</span></label>)}</div> : <p className="text-sm text-[#66726B]">Chưa có nguyên liệu chi tiết.</p>}
    </section>

    <section className="space-y-5 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm sm:p-8"><div className="border-b border-[#E5DECE] pb-4"><h2 className="font-editorial text-xl font-bold">Các Bước Thực Hiện</h2><p className="mt-1 text-xs text-[#66726B]">Theo dõi từng bước khi nấu.</p></div>
      {recipe.steps.length ? <div className="space-y-4">{recipe.steps.map(step => <div key={step.id} className={`rounded-xl border p-5 ${checkedSteps.includes(step.id) ? "border-[#C6A15B]/40 bg-[#063C2F]/5" : "border-[#E5DECE] bg-[#F5F0E6]"}`}><div className="flex flex-wrap items-center justify-between gap-3"><h3 className="font-editorial text-base font-bold"><span className="mr-2 text-[#A8894D]">{String(step.stepNumber).padStart(2, "0")}.</span>{step.title || `Bước ${step.stepNumber}`}</h3><button type="button" onClick={() => toggle(step.id, checkedSteps, setCheckedSteps)} className={`inline-flex items-center gap-1 rounded-md border px-2.5 py-1 text-xs ${checkedSteps.includes(step.id) ? "border-[#063C2F] bg-[#063C2F] text-white" : "border-[#E5DECE] bg-[#FBF8F1]"}`}><Check className="h-3 w-3" />{checkedSteps.includes(step.id) ? "Đã hoàn thành" : "Đánh dấu"}</button></div><p className="mt-3 text-sm leading-7 text-[#4B5750]">{step.description}</p>{step.durationMinutes != null && <p className="mt-3 flex items-center gap-1 text-xs text-[#66726B]"><Clock3 className="h-3.5 w-3.5" />{step.durationMinutes} phút</p>}</div>)}</div> : <div className="whitespace-pre-line text-sm leading-7 text-[#4B5750]">{recipe.instructions || "Chưa có hướng dẫn chi tiết."}</div>}
    </section>

    {recipe.nutrition && <section className="rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm sm:p-8"><h2 className="font-editorial text-xl font-bold">Thông Tin Dinh Dưỡng</h2><p className="mt-1 text-xs text-[#66726B]">Theo dữ liệu tác giả cung cấp cho công thức.</p><div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">{[{ label: "Năng lượng", value: `${recipe.nutrition.calories} kcal` }, { label: "Đạm", value: `${recipe.nutrition.protein} g` }, { label: "Tinh bột", value: `${recipe.nutrition.carbs} g` }, { label: "Chất béo", value: `${recipe.nutrition.fat} g` }].map(item => <div key={item.label} className="rounded-xl bg-[#F5F0E6] p-4 text-center"><p className="text-xs text-[#66726B]">{item.label}</p><p className="mt-1 font-editorial text-lg font-bold text-[#063C2F]">{item.value}</p></div>)}</div></section>}
  </main>;
}
