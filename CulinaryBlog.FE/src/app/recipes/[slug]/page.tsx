import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { RecipeDetailView } from "../../../components/recipes/RecipeDetailView";
import { recipeApiUrl, recipeImageUrl, type RecipeDetail } from "../../../lib/recipes";

type Props = { params: Promise<{ slug: string }> };
type PublicRecipeResult = { kind: "published"; recipe: RecipeDetail } | { kind: "private" | "missing" | "unavailable" };

export const revalidate = 300;

async function getPublicRecipe(slug: string): Promise<PublicRecipeResult> {
  try {
    const response = await fetch(`${recipeApiUrl}/v1/recipes/${encodeURIComponent(slug)}`, {
      next: { revalidate: 300 },
      signal: AbortSignal.timeout(10000),
    });
    if (response.status === 404) return { kind: "missing" };
    if (response.status === 403) return { kind: "private" };
    if (!response.ok) return { kind: "unavailable" };
    const recipe = await response.json() as RecipeDetail;
    return recipe.status === "Published" ? { kind: "published", recipe } : { kind: "private" };
  } catch {
    return { kind: "unavailable" };
  }
}

function recipeUrl(slug: string) {
  const origin = (process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000").replace(/\/$/, "");
  return `${origin}/recipes/${encodeURIComponent(slug)}`;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const result = await getPublicRecipe(slug);
  if (result.kind !== "published") return { title: "Chi tiết công thức | CulinaryBlog", robots: { index: false, follow: false } };
  const { recipe } = result;
  const description = (recipe.description || `Cách làm ${recipe.title} với nguyên liệu và các bước thực hiện chi tiết.`).slice(0, 160);
  const image = recipeImageUrl(recipe.images.find(item => item.isPrimary)?.originalUrl ?? recipe.imageUrl);
  const url = recipeUrl(recipe.slug);
  return {
    title: `${recipe.title} | Culinary Blog`,
    description,
    alternates: { canonical: url },
    robots: { index: true, follow: true },
    openGraph: { title: recipe.title, description, url, type: "article", images: image ? [image] : [] },
    twitter: { card: "summary_large_image", title: recipe.title, description, images: image ? [image] : [] },
  };
}

export default async function RecipeDetailPage({ params }: Props) {
  const { slug } = await params;
  const result = await getPublicRecipe(slug);
  if (result.kind === "missing") notFound();
  if (result.kind !== "published") return <RecipeDetailView slug={slug} />;

  const { recipe } = result;
  const images = recipe.images.map(item => recipeImageUrl(item.originalUrl)).filter((url): url is string => Boolean(url));
  const structuredData = {
    "@context": "https://schema.org",
    "@type": "Recipe",
    name: recipe.title,
    description: recipe.description || `Cách làm ${recipe.title} với nguyên liệu và các bước thực hiện chi tiết.`,
    image: images.length ? images : recipeImageUrl(recipe.imageUrl) ? [recipeImageUrl(recipe.imageUrl)] : undefined,
    author: recipe.author ? { "@type": "Person", name: recipe.author.fullName || recipe.author.userName } : undefined,
    datePublished: recipe.publishedAt || undefined,
    prepTime: `PT${recipe.prepTimeMinutes}M`,
    cookTime: `PT${recipe.cookingTimeMinutes}M`,
    totalTime: `PT${recipe.prepTimeMinutes + recipe.cookingTimeMinutes}M`,
    recipeYield: String(recipe.servings),
    recipeCategory: recipe.categoryName || undefined,
    recipeIngredient: recipe.ingredients.map(item => [item.quantity, item.unit, item.name].filter(value => value !== null && value !== "").join(" ")),
    recipeInstructions: recipe.steps.map(step => ({ "@type": "HowToStep", name: step.title || undefined, text: step.description, image: step.imageUrl ? recipeImageUrl(step.imageUrl) : undefined })),
    nutrition: recipe.nutrition ? {
      "@type": "NutritionInformation",
      calories: `${recipe.nutrition.calories} kcal`,
      proteinContent: `${recipe.nutrition.protein} g`,
      carbohydrateContent: `${recipe.nutrition.carbs} g`,
      fatContent: `${recipe.nutrition.fat} g`,
      fiberContent: recipe.nutrition.fiber == null ? undefined : `${recipe.nutrition.fiber} g`,
      sodiumContent: recipe.nutrition.sodium == null ? undefined : `${recipe.nutrition.sodium} mg`,
    } : undefined,
  };

  return <>
    <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData).replace(/</g, "\\u003c") }} />
    <RecipeDetailView slug={slug} initialRecipe={recipe} />
  </>;
}
