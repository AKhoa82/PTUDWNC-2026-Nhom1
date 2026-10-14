
import type { Metadata } from "next";

import "./globals.css";

import Providers from "./providers";
import { SiteShell } from "../components/layout/SiteShell";

export const metadata: Metadata = {
  title: "CulinaryBlog | Bản sắc ẩm thực Việt",
  description:
    "Khám phá, lưu giữ và chia sẻ những công thức nấu ăn Việt Nam.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="vi">
      <body className="min-h-screen w-full bg-[#FBF8F1] font-sans text-[#1C2421] selection:bg-[#C6A15B]/25 selection:text-[#022C24]">

        <Providers>
          <SiteShell>
            {children}
          </SiteShell>
        </Providers>

      </body>
    </html>
  );
}