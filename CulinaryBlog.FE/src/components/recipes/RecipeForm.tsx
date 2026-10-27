"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { ArrowLeft, ChefHat, ImageIcon, Plus, Save, Trash2 } from "lucide-react";
import { AUTH_CHANGED_EVENT, ensureFreshSession, readAuthSession, type AuthSession } from "../../lib/auth-session";
import { authHeaders, recipeImageUrl, recipeRequest, RecipeRequestError, type PagedRecipes, type RecipeCategory, type RecipeDetail } from "../../lib/recipes";
import { RecipeImageManager } from "./RecipeImageManager";

const ingredientSchema = z.object({
  serverId: z.string().optional(),
  name: z.string().trim().min(1, "Nhập tên nguyên liệu.").max(100),
  quantity: z.string().refine(value => value === "" || (Number.isFinite(Number(value)) && Number(value) > 0), "Số lượng phải lớn hơn 0."),
  unit: z.string(), notes: z.string(),
});
const stepSchema = z.object({
  serverId: z.string().optional(),
  title: z.string(),
  description: z.string().trim().min(1, "Nhập hướng dẫn cho bước này.").max(2000),
  durationMinutes: z.string().refine(value => value === "" || (Number.isInteger(Number(value)) && Number(value) >= 0), "Thời gian không hợp lệ."),
  imageUrl: z.string().refine(value => !value || /^https?:\/\//i.test(value) || value.startsWith("/"), "Nhập URL ảnh hợp lệ."),
});
const schema = z.object({
  title: z.string().trim().min(5, "Tiêu đề phải có ít nhất 5 ký tự.").max(200)
    .refine(value => /[a-z0-9]/i.test(value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[đĐ]/g, "d")), "Tiêu đề phải chứa ký tự có thể tạo đường dẫn công thức."),
  categoryId: z.guid("Chọn danh mục món ăn."),
  description: z.string().trim().min(1, "Nhập mô tả công thức.").max(2000, "Mô tả không được vượt quá 2000 ký tự."),
  imageUrl: z.string().max(500, "Đường dẫn ảnh quá dài.").refine(value => !value || /^https?:\/\//i.test(value) || value.startsWith("/"), "Nhập URL ảnh hợp lệ."),
  difficulty: z.enum(["1", "2", "3", "4"]),
  prepTimeMinutes: z.number().int().min(1, "Ít nhất 1 phút.").max(1000),
  cookingTimeMinutes: z.number().int().min(0).max(1000),
  servings: z.number().int().min(1).max(100),
  calories: z.number().min(0), protein: z.number().min(0), carbs: z.number().min(0), fat: z.number().min(0),
  fiber: z.string().refine(value => value === "" || (Number.isFinite(Number(value)) && Number(value) >= 0), "Chất xơ không hợp lệ."),
  sodium: z.string().refine(value => value === "" || (Number.isFinite(Number(value)) && Number(value) >= 0), "Natri không hợp lệ."),
  ingredients: z.array(ingredientSchema),
  steps: z.array(stepSchema),
});
type FormValues = z.infer<typeof schema>;

const emptyValues: FormValues = {
  title: "", categoryId: "", description: "", imageUrl: "", difficulty: "1", prepTimeMinutes: 15,
  cookingTimeMinutes: 30, servings: 4, calories: 0, protein: 0, carbs: 0, fat: 0, fiber: "", sodium: "",
  ingredients: [],
  steps: [],
};
const inputClass = "w-full rounded-lg border border-[#E5DECE] bg-[#F5F0E6] px-3 py-2.5 text-sm text-[#1C2421] outline-none focus:border-[#C6A15B] focus:bg-[#FBF8F1] focus:ring-2 focus:ring-[#C6A15B]/20";
const labelClass = "mb-1.5 block text-xs font-semibold text-[#4B5750]";
const panelClass = "space-y-5 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm sm:p-8";

function formValuesFromRecipe(recipe: RecipeDetail): FormValues {
  const difficulty = { Easy: "1", Medium: "2", Hard: "3", Expert: "4" }[recipe.difficulty] ?? "1";
  return {
    title: recipe.title, categoryId: recipe.categoryId, description: recipe.description ?? "", imageUrl: recipe.imageUrl ?? "",
    difficulty: difficulty as FormValues["difficulty"], prepTimeMinutes: recipe.prepTimeMinutes,
    cookingTimeMinutes: recipe.cookingTimeMinutes, servings: recipe.servings,
    calories: recipe.nutrition?.calories ?? 0, protein: recipe.nutrition?.protein ?? 0,
    carbs: recipe.nutrition?.carbs ?? 0, fat: recipe.nutrition?.fat ?? 0,
    fiber: recipe.nutrition?.fiber == null ? "" : String(recipe.nutrition.fiber),
    sodium: recipe.nutrition?.sodium == null ? "" : String(recipe.nutrition.sodium),
    ingredients: recipe.ingredients.map(item => ({ serverId: item.id, name: item.name, quantity: item.quantity == null ? "" : String(item.quantity), unit: item.unit ?? "", notes: item.notes ?? "" })),
    steps: recipe.steps.map(item => ({ serverId: item.id, title: item.title ?? "", description: item.description, durationMinutes: item.durationMinutes == null ? "" : String(item.durationMinutes), imageUrl: item.imageUrl ?? "" })),
  };
}

async function resolveRecipeSlug(id: string) {
  for (let page = 1; page <= 100; page++) {
    const result = await recipeRequest<PagedRecipes>(`/v1/recipes?page=${page}&pageSize=50`, { headers: authHeaders(), cache: "no-store" });
    const found = result.items.find(item => item.id === id);
    if (found) return found.slug;
    if (!result.hasNextPage) break;
  }
  throw new Error("Không tìm thấy công thức cần chỉnh sửa.");
}

async function uploadCover(id: string, file: File, title: string) {
  const data = new FormData();
  data.append("file", file);
  data.append("altText", title);
  data.append("isPrimary", "true");
  await recipeRequest(`/v1/recipes/${id}/images/`, { method: "POST", headers: authHeaders(), body: data });
}

export function RecipeForm({ recipeId, recipeSlug, initialNotice }: { recipeId?: string; recipeSlug?: string; initialNotice?: string }) {
  const isEdit = Boolean(recipeId);
  const router = useRouter();
  const queryClient = useQueryClient();
  const publishIntent = useRef(false);
  const [session, setSession] = useState<AuthSession | null>(null);
  const [ready, setReady] = useState(false);
  const [saveError, setSaveError] = useState(initialNotice ?? "");
  const [loginRequired, setLoginRequired] = useState(false);
  const [saving, setSaving] = useState(false);
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [hydratedRecipeId, setHydratedRecipeId] = useState<string | null>(null);
  const [activeSection, setActiveSection] = useState(1);
  const [furthestSection, setFurthestSection] = useState(1);

  useEffect(() => {
    const sync = () => { setSession(readAuthSession()); setReady(true); };
    sync(); window.addEventListener("storage", sync); window.addEventListener(AUTH_CHANGED_EVENT, sync);
    return () => { window.removeEventListener("storage", sync); window.removeEventListener(AUTH_CHANGED_EVENT, sync); };
  }, []);
  const allowed = session?.role === "admin" || session?.role === "author";
  const categoriesQuery = useQuery({ queryKey: ["recipe-categories"], queryFn: () => recipeRequest<RecipeCategory[]>("/v1/categories", { cache: "no-store" }), enabled: ready && allowed });
  const detailQuery = useQuery({
    queryKey: ["edit-recipe", recipeId, recipeSlug, session?.id],
    queryFn: async () => {
      const slug = recipeSlug || await resolveRecipeSlug(recipeId!);
      const result = await recipeRequest<RecipeDetail>(`/v1/recipes/${encodeURIComponent(slug)}`, { headers: authHeaders(), cache: "no-store" });
      if (result.id !== recipeId) throw new Error("Đường dẫn công thức không khớp dữ liệu.");
      return result;
    },
    enabled: ready && allowed && isEdit,
  });
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: emptyValues });
  const ingredientFields = useFieldArray({ control: form.control, name: "ingredients", keyName: "fieldKey" });
  const stepFields = useFieldArray({ control: form.control, name: "steps", keyName: "fieldKey" });
  const imageUrl = form.watch("imageUrl");

  async function nextSection() {
    const fields = activeSection === 1 ? ["title", "categoryId", "description", "imageUrl", "difficulty"] as const
      : activeSection === 2 ? ["prepTimeMinutes", "cookingTimeMinutes", "servings"] as const
      : activeSection === 3 ? ["ingredients"] as const
      : ["steps"] as const;
    if (!await form.trigger(fields)) return;
    const next = Math.min(activeSection + 1, 5);
    setActiveSection(next);
    setFurthestSection(previous => Math.max(previous, next));
    setSaveError("");
  }

  function showFirstInvalidSection(errors: typeof form.formState.errors) {
    if (errors.title || errors.categoryId || errors.description || errors.imageUrl || errors.difficulty) setActiveSection(1);
    else if (errors.prepTimeMinutes || errors.cookingTimeMinutes || errors.servings) setActiveSection(2);
    else if (errors.ingredients) setActiveSection(3);
    else if (errors.steps) setActiveSection(4);
    else setActiveSection(5);
  }

  useEffect(() => {
    if (detailQuery.data && hydratedRecipeId !== detailQuery.data.id) {
      form.reset(formValuesFromRecipe(detailQuery.data));
      setHydratedRecipeId(detailQuery.data.id);
    }
  }, [detailQuery.data, form, hydratedRecipeId]);

  async function syncIngredients(id: string, original: RecipeDetail, values: FormValues) {
    const currentIds = new Set(values.ingredients.map(item => item.serverId).filter(Boolean));
    for (const [index, item] of values.ingredients.entries()) {
      const payload = { name: item.name.trim(), quantity: item.quantity ? Number(item.quantity) : null, unit: item.unit || null, notes: item.notes || null, sortOrder: index };
      await recipeRequest<void>(`/v1/recipes/${id}/ingredients${item.serverId ? `/${item.serverId}` : ""}`, { method: item.serverId ? "PUT" : "POST", headers: { ...authHeaders(), "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    }
    for (const old of original.ingredients) if (!currentIds.has(old.id)) await recipeRequest<void>(`/v1/recipes/${id}/ingredients/${old.id}`, { method: "DELETE", headers: authHeaders() });
  }
  async function syncSteps(id: string, original: RecipeDetail, values: FormValues) {
    const currentIds = new Set(values.steps.map(item => item.serverId).filter(Boolean));
    for (const item of values.steps) {
      const payload = { title: item.title || null, description: item.description.trim(), durationMinutes: item.durationMinutes ? Number(item.durationMinutes) : null, imageUrl: item.imageUrl.trim() || null };
      await recipeRequest<void>(`/v1/recipes/${id}/steps/${item.serverId ?? ""}`, { method: item.serverId ? "PUT" : "POST", headers: { ...authHeaders(), "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    }
    for (const old of original.steps) if (!currentIds.has(old.id)) await recipeRequest<void>(`/v1/recipes/${id}/steps/${old.id}`, { method: "DELETE", headers: authHeaders() });
  }

  async function submit(values: FormValues) {
    if (!isEdit && publishIntent.current && (!values.ingredients.length || !values.steps.length)) {
      setSaveError("Để xuất bản, công thức cần ít nhất một nguyên liệu và một bước nấu. Bạn vẫn có thể lưu bản nháp.");
      setActiveSection(values.ingredients.length ? 4 : 3);
      return;
    }
    if (isEdit && detailQuery.data?.status === "Published" && (!values.ingredients.length || !values.steps.length)) {
      setSaveError("Công thức đã xuất bản cần ít nhất một nguyên liệu và một bước nấu. Hãy hủy xuất bản trước nếu muốn xóa hết.");
      return;
    }
    if (!readAuthSession() && !await ensureFreshSession()) {
      setSaveError("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại trong tab mới, sau đó quay về đây để lưu công thức.");
      setLoginRequired(true);
      return;
    }
    setSaving(true); setSaveError("");
    setLoginRequired(false);
    let primarySaved = false;
    let createdRecipe: { id: string; slug: string } | null = null;
    const nutrition = values.calories || values.protein || values.carbs || values.fat || values.fiber || values.sodium ? { calories: values.calories, protein: values.protein, carbs: values.carbs, fat: values.fat, fiber: values.fiber === "" ? null : Number(values.fiber), sodium: values.sodium === "" ? null : Number(values.sodium) } : null;
    const basePayload = { title: values.title.trim(), description: values.description.trim() || null, prepTimeMinutes: values.prepTimeMinutes, cookingTimeMinutes: values.cookingTimeMinutes, servings: values.servings, difficulty: Number(values.difficulty), instructions: values.steps.map((step, index) => `${index + 1}. ${step.description.trim()}`).join("\n"), categoryId: values.categoryId, nutrition };
    try {
      if (isEdit && recipeId && detailQuery.data) {
        await recipeRequest(`/v1/recipes/${recipeId}`, { method: "PUT", headers: { ...authHeaders(), "Content-Type": "application/json" }, body: JSON.stringify({ ...basePayload, rowVersion: detailQuery.data.rowVersion }) });
        primarySaved = true;
        await syncIngredients(recipeId, detailQuery.data, values);
        await syncSteps(recipeId, detailQuery.data, values);
        if (coverFile) await uploadCover(recipeId, coverFile, values.title);
      } else {
        const created = await recipeRequest<{ id: string; slug: string }>("/v1/recipes", { method: "POST", headers: { ...authHeaders(), "Content-Type": "application/json" }, body: JSON.stringify({ ...basePayload, imageUrl: values.imageUrl.trim() || null, ingredients: values.ingredients.map((item, index) => ({ name: item.name.trim(), quantity: item.quantity ? Number(item.quantity) : null, unit: item.unit || null, notes: item.notes || null, sortOrder: index })), steps: values.steps.map(item => ({ title: item.title || null, description: item.description.trim(), durationMinutes: item.durationMinutes ? Number(item.durationMinutes) : null, imageUrl: item.imageUrl.trim() || null })) }) });
        createdRecipe = created;
        primarySaved = true;
        if (coverFile) await uploadCover(created.id, coverFile, values.title);
        if (publishIntent.current) await recipeRequest(`/v1/recipes/${created.id}/publish`, { method: "PATCH", headers: authHeaders() });
      }
      await queryClient.invalidateQueries({ queryKey: ["managed-recipes"] });
      await queryClient.invalidateQueries({ queryKey: ["recipe"] });
      router.push("/dashboard/recipes");
      router.refresh();
    } catch (error) {
      if (createdRecipe) {
        router.push(`/dashboard/recipes/${createdRecipe.id}/edit?slug=${encodeURIComponent(createdRecipe.slug)}&notice=draft`);
        return;
      }
      if (error instanceof RecipeRequestError && error.status === 401) {
        setSaveError("Phiên đăng nhập không còn hợp lệ. Hãy đăng nhập lại trong tab mới, sau đó quay về đây để lưu công thức.");
        setLoginRequired(true);
        setSaving(false);
        return;
      }
      setSaveError(`${error instanceof Error ? error.message : "Không thể lưu công thức."}${primarySaved ? " Một phần dữ liệu có thể đã được lưu; hãy tải lại trang để kiểm tra." : ""}`);
      if (primarySaved) { setHydratedRecipeId(null); await queryClient.invalidateQueries({ queryKey: ["edit-recipe"] }); }
      setSaving(false);
    }
  }

  if (!ready) return <div className="mx-auto max-w-4xl p-8 text-sm text-[#66726B]">Đang kiểm tra tài khoản...</div>;
  if (!allowed) return <div className="mx-auto max-w-4xl space-y-4 rounded-2xl border border-[#E5DECE] bg-[#FBF8F1] p-8"><h1 className="font-editorial text-2xl font-bold">Biên tập công thức</h1><p className="text-sm text-[#66726B]">Vui lòng đăng nhập bằng tài khoản tác giả hoặc quản trị viên.</p><Link href="/auth/login" className="inline-block rounded-lg bg-[#063C2F] px-4 py-2 text-sm font-semibold text-white">Đăng nhập</Link></div>;
  if (isEdit && detailQuery.isPending) return <div className="mx-auto max-w-4xl p-8 text-sm text-[#66726B]">Đang tải công thức...</div>;
  if (isEdit && detailQuery.isError) return <div className="mx-auto max-w-4xl space-y-4 p-8"><Link href="/dashboard/recipes" className="text-xs font-semibold text-[#063C2F]">Quay lại danh sách</Link><p role="alert" className="rounded-xl border border-[#B9674A]/30 bg-[#B9674A]/10 p-4 text-sm text-[#8D3C25]">{detailQuery.error.message}</p></div>;
  if (isEdit && detailQuery.data && detailQuery.data.authorId !== session?.id && session?.role !== "admin") return <div className="mx-auto max-w-4xl p-8 text-sm text-[#8D3C25]">Bạn không có quyền chỉnh sửa công thức này.</div>;

  return <div className="mx-auto max-w-4xl space-y-8 pb-16">
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#E5DECE] pb-4"><Link href="/dashboard/recipes" className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#66726B] hover:text-[#063C2F]"><ArrowLeft className="h-4 w-4" />Quay lại danh sách công thức</Link><span className="text-[10px] font-semibold uppercase tracking-wider text-[#A8894D]">Biên tập công thức</span></div>
    <header><p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-[#A8894D]">{isEdit ? "Hiệu chỉnh món ăn" : "Sáng tác món mới"}</p><h1 className="mt-1 font-editorial text-3xl font-bold text-[#1C2421]">{isEdit ? "Chỉnh Sửa Công Thức Nấu Ăn" : "Tạo Công Thức Nấu Ăn Mới"}</h1><p className="mt-2 text-sm text-[#66726B]">Điền thông tin, nguyên liệu và các bước nấu cho công thức.</p></header>
    {saveError && <div role="alert" className="rounded-xl border border-[#B9674A]/30 bg-[#B9674A]/10 p-4 text-sm text-[#8D3C25]">{saveError}{loginRequired && <a href="/auth/login" target="_blank" rel="noopener noreferrer" className="ml-2 font-semibold underline">Mở trang đăng nhập</a>}</div>}
    {!isEdit && <nav aria-label="Tiến độ tạo công thức" className="flex flex-wrap gap-2">{["Thông tin", "Thời gian", "Nguyên liệu", "Các bước", "Dinh dưỡng"].map((label, index) => <button key={label} type="button" disabled={index + 1 > furthestSection} onClick={() => setActiveSection(index + 1)} aria-current={activeSection === index + 1 ? "step" : undefined} className={`rounded-lg px-3 py-2 text-xs font-semibold disabled:opacity-40 ${activeSection === index + 1 ? "bg-[#063C2F] text-white" : "border border-[#E5DECE] bg-[#FBF8F1] text-[#4B5750]"}`}>{index + 1}. {label}</button>)}</nav>}
    <form onSubmit={form.handleSubmit(submit, showFirstInvalidSection)} className="space-y-8">
      <section hidden={!isEdit && activeSection !== 1} className={panelClass}><h2 className="border-b border-[#E5DECE] pb-3 font-editorial text-lg font-bold">1. Thông tin cơ bản</h2><div className="grid gap-4 md:grid-cols-2">
        <label className="md:col-span-2"><span className={labelClass}>Tiêu đề công thức *</span><input {...form.register("title")} placeholder="Ví dụ: Bún bò Huế" className={inputClass} /><ErrorText text={form.formState.errors.title?.message} /></label>
        <label><span className={labelClass}>Danh mục món ăn *</span><select {...form.register("categoryId")} className={inputClass}><option value="">Chọn danh mục</option>{categoriesQuery.data?.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select><ErrorText text={form.formState.errors.categoryId?.message} />{categoriesQuery.isError && <ErrorText text="Không thể tải danh mục. Kiểm tra backend." />}</label>
        <label><span className={labelClass}>Độ khó *</span><select {...form.register("difficulty")} className={inputClass}><option value="1">Dễ</option><option value="2">Trung bình</option><option value="3">Khó</option><option value="4">Chuyên gia</option></select></label>
        <label className="md:col-span-2"><span className={labelClass}>Mô tả món ăn *</span><textarea {...form.register("description")} rows={3} maxLength={2000} placeholder="Giới thiệu hương vị và nguồn gốc món ăn..." className={inputClass} /><ErrorText text={form.formState.errors.description?.message} /></label>
        <div className="md:col-span-2"><label><span className={labelClass}>Ảnh bìa công thức (URL)</span><span className="relative block"><ImageIcon className="absolute left-3 top-3 h-4 w-4 text-[#A8894D]" /><input {...form.register("imageUrl")} readOnly={isEdit} placeholder="https://..." className={`${inputClass} pl-10 ${isEdit ? "cursor-not-allowed opacity-70" : ""}`} /></span><ErrorText text={form.formState.errors.imageUrl?.message} /></label><label className="mt-3 block"><span className={labelClass}>{isEdit ? "Thay ảnh bìa" : "Hoặc tải ảnh bìa lên"}</span><input type="file" accept="image/jpeg,image/png,image/webp,image/avif" onChange={event => { const file = event.target.files?.[0] ?? null; if (file && file.size > 5 * 1024 * 1024) { setSaveError("Ảnh bìa không được vượt quá 5 MB."); event.target.value = ""; setCoverFile(null); } else { setSaveError(""); setCoverFile(file); } }} className="block w-full rounded-lg border border-[#E5DECE] bg-[#F5F0E6] p-2 text-xs" /><span className="mt-1 block text-xs text-[#66726B]">JPG, PNG, WebP hoặc AVIF; tối đa 5 MB.</span></label>{imageUrl && <div className="mt-3 h-44 overflow-hidden rounded-xl border border-[#E5DECE] bg-[#F5F0E6]"><img src={recipeImageUrl(imageUrl) ?? ""} alt="Xem trước ảnh bìa" className="h-full w-full object-cover" /></div>}</div>
      </div></section>
      <section hidden={!isEdit && activeSection !== 2} className={panelClass}><h2 className="border-b border-[#E5DECE] pb-3 font-editorial text-lg font-bold">2. Định lượng &amp; thời gian</h2><div className="grid gap-4 sm:grid-cols-3">
        <label><span className={labelClass}>Chuẩn bị (phút) *</span><input type="number" min="1" max="1000" {...form.register("prepTimeMinutes", { valueAsNumber: true })} className={inputClass} /><ErrorText text={form.formState.errors.prepTimeMinutes?.message} /></label>
        <label><span className={labelClass}>Nấu chín (phút) *</span><input type="number" min="0" max="1000" {...form.register("cookingTimeMinutes", { valueAsNumber: true })} className={inputClass} /><ErrorText text={form.formState.errors.cookingTimeMinutes?.message} /></label>
        <label><span className={labelClass}>Khẩu phần *</span><input type="number" min="1" max="100" {...form.register("servings", { valueAsNumber: true })} className={inputClass} /><ErrorText text={form.formState.errors.servings?.message} /></label>
      </div></section>
      <section hidden={!isEdit && activeSection !== 3} className={panelClass}><div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#E5DECE] pb-3"><h2 className="font-editorial text-lg font-bold">3. Danh sách nguyên liệu ({ingredientFields.fields.length})</h2><button type="button" onClick={() => ingredientFields.append({ name: "", quantity: "", unit: "", notes: "" })} className="inline-flex items-center gap-1 rounded-lg border border-[#C6A15B]/50 px-3 py-2 text-xs font-semibold text-[#063C2F]"><Plus className="h-3.5 w-3.5" />Thêm nguyên liệu</button></div><div className="space-y-3">{ingredientFields.fields.map((field, index) => <div key={field.fieldKey} className="grid gap-2 rounded-xl border border-[#E5DECE] bg-[#F5F0E6] p-3 sm:grid-cols-[2fr_1fr_1fr_2fr_auto]"><label><span className="sr-only">Tên nguyên liệu {index + 1}</span><input {...form.register(`ingredients.${index}.name`)} placeholder="Tên nguyên liệu *" className={inputClass} /><ErrorText text={form.formState.errors.ingredients?.[index]?.name?.message} /></label><label><span className="sr-only">Số lượng {index + 1}</span><input {...form.register(`ingredients.${index}.quantity`)} type="number" min="0.01" step="any" placeholder="Số lượng" className={inputClass} /><ErrorText text={form.formState.errors.ingredients?.[index]?.quantity?.message} /></label><label><span className="sr-only">Đơn vị {index + 1}</span><input {...form.register(`ingredients.${index}.unit`)} placeholder="Đơn vị" className={inputClass} /></label><label><span className="sr-only">Ghi chú {index + 1}</span><input {...form.register(`ingredients.${index}.notes`)} placeholder="Ghi chú" className={inputClass} /></label><button type="button" aria-label={`Xóa nguyên liệu ${index + 1}`} onClick={() => ingredientFields.remove(index)} className="rounded-lg p-2 text-[#B9674A]"><Trash2 className="h-4 w-4" /></button></div>)}</div><ErrorText text={form.formState.errors.ingredients?.root?.message} /></section>
      <section hidden={!isEdit && activeSection !== 4} className={panelClass}><div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#E5DECE] pb-3"><h2 className="font-editorial text-lg font-bold">4. Các bước thực hiện ({stepFields.fields.length})</h2><button type="button" onClick={() => stepFields.append({ title: "", description: "", durationMinutes: "", imageUrl: "" })} className="inline-flex items-center gap-1 rounded-lg border border-[#C6A15B]/50 px-3 py-2 text-xs font-semibold text-[#063C2F]"><Plus className="h-3.5 w-3.5" />Thêm bước nấu</button></div><div className="space-y-4">{stepFields.fields.map((field, index) => <div key={field.fieldKey} className="space-y-3 rounded-xl border border-[#E5DECE] bg-[#F5F0E6] p-4"><div className="flex items-center justify-between"><span className="rounded-full border border-[#C6A15B]/30 bg-[#FBF8F1] px-3 py-1 text-[10px] font-semibold uppercase text-[#063C2F]">Bước {index + 1}</span><button type="button" aria-label={`Xóa bước ${index + 1}`} onClick={() => stepFields.remove(index)} className="rounded-lg p-2 text-[#B9674A]"><Trash2 className="h-4 w-4" /></button></div><div className="grid gap-3 sm:grid-cols-[2fr_1fr]"><label><span className="sr-only">Tiêu đề bước {index + 1}</span><input {...form.register(`steps.${index}.title`)} placeholder="Tiêu đề bước nấu" className={inputClass} /></label><label><span className="sr-only">Thời gian bước {index + 1}</span><input {...form.register(`steps.${index}.durationMinutes`)} type="number" min="0" placeholder="Thời gian (phút)" className={inputClass} /><ErrorText text={form.formState.errors.steps?.[index]?.durationMinutes?.message} /></label></div><label><span className="sr-only">Mô tả bước {index + 1}</span><textarea {...form.register(`steps.${index}.description`)} rows={3} placeholder="Mô tả chi tiết thao tác của bước này..." className={inputClass} /><ErrorText text={form.formState.errors.steps?.[index]?.description?.message} /></label><label><span className={labelClass}>Ảnh minh họa bước (URL)</span><input {...form.register(`steps.${index}.imageUrl`)} placeholder="https://..." className={inputClass} /><ErrorText text={form.formState.errors.steps?.[index]?.imageUrl?.message} /></label></div>)}</div><ErrorText text={form.formState.errors.steps?.root?.message} /></section>
      <section hidden={!isEdit && activeSection !== 5} className={panelClass}><h2 className="flex items-center gap-2 border-b border-[#E5DECE] pb-3 font-editorial text-lg font-bold"><ChefHat className="h-5 w-5 text-[#A8894D]" />5. Thông tin dinh dưỡng (tùy chọn)</h2><div className="grid gap-4 sm:grid-cols-3">{([{ key: "calories", label: "Năng lượng (kcal)" }, { key: "protein", label: "Đạm (g)" }, { key: "carbs", label: "Tinh bột (g)" }, { key: "fat", label: "Chất béo (g)" }] as const).map(item => <label key={item.key}><span className={labelClass}>{item.label}</span><input type="number" min="0" step="any" {...form.register(item.key, { valueAsNumber: true })} className={inputClass} /><ErrorText text={form.formState.errors[item.key]?.message} /></label>)}{([{ key: "fiber", label: "Chất xơ (g)" }, { key: "sodium", label: "Natri (mg)" }] as const).map(item => <label key={item.key}><span className={labelClass}>{item.label}</span><input type="number" min="0" step="any" {...form.register(item.key)} className={inputClass} /><ErrorText text={form.formState.errors[item.key]?.message} /></label>)}</div></section>
      {!isEdit && <div className="flex items-center justify-between gap-3"><button type="button" disabled={activeSection === 1} onClick={() => setActiveSection(previous => Math.max(1, previous - 1))} className="rounded-lg border border-[#E5DECE] px-4 py-2 text-sm font-semibold text-[#063C2F] disabled:opacity-40">Bước trước</button><span className="text-xs text-[#66726B]">Bước {activeSection} / 5</span>{activeSection < 5 ? <button type="button" onClick={nextSection} className="rounded-lg bg-[#063C2F] px-4 py-2 text-sm font-semibold text-white">Bước tiếp theo</button> : <span />}</div>}
      <div className="flex flex-wrap justify-end gap-3 border-t border-[#E5DECE] pt-5"><Link href="/dashboard/recipes" className="rounded-lg border border-[#E5DECE] px-4 py-2.5 text-sm font-semibold text-[#063C2F] hover:bg-[#FBF8F1]">Hủy thao tác</Link>{!isEdit && <button type="submit" disabled={saving || categoriesQuery.isPending || categoriesQuery.isError} onClick={() => { publishIntent.current = false; }} className="inline-flex items-center gap-2 rounded-lg border border-[#C6A15B]/50 bg-[#FBF8F1] px-4 py-2.5 text-sm font-semibold text-[#063C2F] disabled:opacity-40"><Save className="h-4 w-4" />Lưu bản nháp</button>}{(isEdit || activeSection === 5) && <button type="submit" disabled={saving || categoriesQuery.isPending || categoriesQuery.isError} onClick={() => { publishIntent.current = true; }} className="inline-flex items-center gap-2 rounded-lg bg-[#C6A15B] px-4 py-2.5 text-sm font-semibold text-[#022C24] disabled:opacity-40"><Save className="h-4 w-4" />{saving ? "Đang lưu..." : isEdit ? "Lưu thay đổi" : "Lưu và xuất bản"}</button>}</div>
    </form>
    {isEdit && recipeId && detailQuery.data && <RecipeImageManager recipeId={recipeId} images={detailQuery.data.images} onChanged={async () => { const updated = await detailQuery.refetch(); if (updated.data) form.setValue("imageUrl", updated.data.imageUrl ?? ""); await queryClient.invalidateQueries({ queryKey: ["managed-recipes"] }); await queryClient.invalidateQueries({ queryKey: ["recipe"] }); }} />}
  </div>;
}

function ErrorText({ text }: { text?: string }) { return text ? <p className="mt-1 text-xs font-medium text-[#B9674A]">{text}</p> : null; }
