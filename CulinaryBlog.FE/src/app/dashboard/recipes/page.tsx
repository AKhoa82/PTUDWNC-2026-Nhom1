import type { Metadata } from "next";
import { RecipeManager } from "../../../components/recipes/RecipeManager";

export const metadata: Metadata = { title: "Quản lý công thức | CulinaryBlog" };
export default function DashboardRecipesPage() { return <RecipeManager />; }
