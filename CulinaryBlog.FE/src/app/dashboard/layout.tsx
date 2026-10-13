import type { ReactNode } from "react";
import { DashboardSidebar } from "../../components/layout/DashboardSidebar";

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-[calc(100vh-72px)] flex-col bg-[#F5F0E6] md:flex-row">
      <DashboardSidebar />
      <main className="min-w-0 flex-1 px-5 py-8 sm:px-8">{children}</main>
    </div>
  );
}
