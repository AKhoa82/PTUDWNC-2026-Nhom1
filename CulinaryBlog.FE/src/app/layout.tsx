import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "CulinaryBlog | Đăng ký",
  description: "Tham gia cộng đồng yêu ẩm thực của CulinaryBlog.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="vi">
      <body className="min-h-screen w-full bg-[#f5f0e6] font-sans selection:bg-[#c6a15b]/25 selection:text-[#022c24]">
        {children}
      </body>
    </html>
  );
}
