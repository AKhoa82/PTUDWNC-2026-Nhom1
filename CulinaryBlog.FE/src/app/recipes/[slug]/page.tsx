import type { Metadata } from "next";
import { RecipeDetailView } from "../../../components/recipes/RecipeDetailView";

export const metadata: Metadata = { title: "Chi tiết công thức | CulinaryBlog" };

export default async function RecipeDetailPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  return <RecipeDetailView slug={slug} />;
}
