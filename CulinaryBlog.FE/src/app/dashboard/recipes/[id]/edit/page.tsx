import type { Metadata } from "next";
import { RecipeForm } from "../../../../../components/recipes/RecipeForm";

export const metadata: Metadata = { title: "Chỉnh sửa công thức | CulinaryBlog" };
export default async function EditRecipePage({ params, searchParams }: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ slug?: string; notice?: string }>;
}) {
  const [{ id }, { slug, notice }] = await Promise.all([params, searchParams]);
  return <RecipeForm recipeId={id} recipeSlug={slug} initialNotice={notice === "draft" ? "Công thức đã được lưu thành bản nháp. Ảnh bìa hoặc bước xuất bản chưa hoàn tất; hãy kiểm tra rồi thử lại." : undefined} />;
}
