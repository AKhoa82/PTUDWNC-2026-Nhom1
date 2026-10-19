import type { Metadata } from "next";
import { RecipeForm } from "../../../../components/recipes/RecipeForm";

export const metadata: Metadata = { title: "Tạo công thức | CulinaryBlog" };
export default function NewRecipePage() { return <RecipeForm />; }
