export const recipeApiUrl = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api").replace(/\/$/, "");

export type RecipeSummary = {
  id: string;
  slug: string;
  title: string;
  description: string | null;
  imageUrl: string | null;
  prepTimeMinutes: number;
  cookingTimeMinutes: number;
  servings: number;
  difficulty: string;
  status: string;
  categoryId: string;
  categoryName: string;
  authorId: string;
  createdAt: string;
  updatedAt: string | null;
};
export type RecipeCategory = { id: string; name: string; slug: string; recipeCount: number };
export type PagedRecipes = {
  items: RecipeSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
};
export type RecipeIngredient = { id: string; name: string; quantity: number | null; unit: string | null; notes: string | null; sortOrder: number };
export type RecipeStep = { id: string; stepNumber: number; title: string | null; description: string; durationMinutes: number | null; imageUrl: string | null };
export type RecipeNutrition = { calories: number; protein: number; carbs: number; fat: number; fiber: number | null; sodium: number | null };
export type RecipeDetail = RecipeSummary & {
  instructions: string;
  rowVersion: string;
  author: { id: string; userName: string; fullName: string } | null;
  ingredients: RecipeIngredient[];
  steps: RecipeStep[];
  nutrition: RecipeNutrition | null;
  images: { imageId: string; originalUrl: string; mediumUrl: string | null; thumbnailUrl: string | null; isPrimary: boolean; altText: string | null }[];
};

export const difficultyLabels: Record<string, string> = { Easy: "Dễ", Medium: "Trung bình", Hard: "Khó", Expert: "Chuyên gia" };
export const statusLabels: Record<string, string> = { Published: "Đã xuất bản", Draft: "Bản nháp", Archived: "Đã lưu trữ" };

export function recipeImageUrl(value: string | null | undefined) {
  if (!value) return null;
  if (/^https?:\/\//i.test(value)) return value;
  const origin = new URL(recipeApiUrl).origin;
  return value.startsWith("/") ? `${origin}${value}` : `${origin}/${value}`;
}

export function formatRecipeDate(value: string | null | undefined) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat("vi-VN").format(date);
}

export class RecipeRequestError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = "RecipeRequestError";
  }
}

export async function recipeRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`${recipeApiUrl}${path}`, options);
  if (!response.ok) {
    let message = `Yêu cầu không thành công (${response.status}).`;
    try {
      const body = await response.json() as { detail?: string; title?: string; errors?: Record<string, string[]> };
      message = body.detail || Object.values(body.errors ?? {}).flat()[0] || body.title || message;
    } catch { /* Keep status message for non-JSON responses. */ }
    throw new RecipeRequestError(message, response.status);
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}

export function authHeaders(): HeadersInit {
  const token = localStorage.getItem("accessToken");
  return token ? { Authorization: `Bearer ${token}` } : {};
}
