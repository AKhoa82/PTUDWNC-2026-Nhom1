
import Link from "next/link";
import {
  ChefHat,
  BookOpen,
  MapPin,
  Heart,
} from "lucide-react";

const footerLink =
  "text-[#F5F0E6]/75 transition-colors hover:text-[#C6A15B]";

export function Footer() {
  return (
    <footer className="border-t border-[#063C2F] bg-[#022C24] pb-9 pt-14 text-[#8D958F]">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">

        <div className="mb-10 grid grid-cols-1 gap-8 md:grid-cols-4">

          {/* CỘT 1: GIỚI THIỆU */}
          <div className="space-y-4 md:col-span-2">

            <Link
              href="/"
              className="inline-flex items-center gap-2.5 text-[#FBF8F1]"
            >
              <div className="flex h-9 w-9 items-center justify-center rounded-lg border border-[#C6A15B]/30 bg-[#063C2F]">
                <ChefHat className="h-5 w-5 text-[#C6A15B]" />
              </div>

              <span className="font-editorial text-xl font-bold tracking-tight">
                Culinary
                <span className="font-normal italic text-[#C6A15B]">
                  Blog
                </span>
              </span>
            </Link>

            <p className="max-w-md text-sm leading-relaxed">
              Nền tảng blog ẩm thực và chia sẻ công thức nấu ăn.
              Không gian lưu giữ bản sắc hương vị truyền thống
              và nghệ thuật ẩm thực Việt Nam.
            </p>

            {/* THÔNG TIN ĐỒ ÁN */}
            <div className="max-w-md space-y-3 rounded-xl border border-[#24604B]/60 bg-[#063C2F]/50 p-4 text-xs text-[#F5F0E6]/85">

              <div className="flex items-center gap-2 font-semibold text-[#C6A15B]">
                <BookOpen className="h-4 w-4 shrink-0" />
                <span>
                  Đồ án Phát triển Ứng dụng Web Nâng cao
                </span>
              </div>

              <div className="flex items-center gap-2">
                <MapPin className="h-4 w-4 shrink-0 text-[#C6A15B]" />
                <span>
                  Khoa Công nghệ Thông tin -
                  Trường Đại học Đà Lạt
                </span>
              </div>

              <p className="text-[#8D958F]">
                Dự án được thực hiện bởi Nhóm 1.
              </p>
            </div>
          </div>

          {/* CỘT 2: KHÁM PHÁ */}
          <div className="space-y-4">
            <h4 className="font-editorial text-xs font-semibold uppercase tracking-widest text-[#C6A15B]">
              Khám phá
            </h4>

            <ul className="space-y-3 text-xs">
              <li>
                <Link href="/recipes" className={footerLink}>
                  Tất cả công thức
                </Link>
              </li>

              <li>
                <Link href="/categories" className={footerLink}>
                  Danh mục món ăn
                </Link>
              </li>

              <li>
                <Link href="/search" className={footerLink}>
                  Tìm kiếm món ngon
                </Link>
              </li>
            </ul>
          </div>

          {/* CỘT 3: HỆ THỐNG */}
          <div className="space-y-4">
            <h4 className="font-editorial text-xs font-semibold uppercase tracking-widest text-[#C6A15B]">
              Hệ thống
            </h4>

            <ul className="space-y-3 text-xs">
              <li>
                <Link href="/auth/login" className={footerLink}>
                  Đăng nhập
                </Link>
              </li>

              <li>
                <Link href="/auth/register" className={footerLink}>
                  Đăng ký tài khoản
                </Link>
              </li>

              <li>
                <span className="text-[#8D958F]">
                  Dashboard (đang phát triển)
                </span>
              </li>

              <li>
                <span className="text-[#8D958F]">
                  Đăng công thức (đang phát triển)
                </span>
              </li>
            </ul>
          </div>
        </div>

        {/* FOOTER BOTTOM */}
        <div className="flex flex-col items-center justify-between gap-4 border-t border-[#063C2F] pt-6 text-center text-xs sm:flex-row sm:text-left">

          <p>
            © {new Date().getFullYear()} CulinaryBlog.
            Bản sắc ẩm thực Việt.
          </p>

          <div className="flex items-center gap-2 text-[#C6A15B]/80">
            <span>Thiết kế với</span>
            <Heart className="h-4 w-4 fill-[#B9674A] text-[#B9674A]" />
            <span>và Next.js</span>
          </div>

        </div>
      </div>
    </footer>
  );
}
