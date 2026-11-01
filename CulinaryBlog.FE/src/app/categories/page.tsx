import Link from "next/link";

interface Category {
  id: string;
  name: string;
  slug: string;
  description?: string;
  createdAt?: string;
}

// Chuyển fetch data sang chạy trên Server
async function getCategories(): Promise<Category[]> {
  const res = await fetch("http://localhost:5018/api/v1/categories", {
    next: { revalidate: 60 }, // Cache 60 giây giúp tải trang siêu tốc
  });
  
  if (!res.ok) throw new Error("Không thể tải danh sách danh mục");
  return res.json();
}

export default async function CategoriesPage() {
  let categories: Category[] = [];
  let error: string | null = null;

  try {
    categories = await getCategories();
  } catch (err: any) {
    error = err.message;
  }

  if (error) {
    return (
      <div className="p-4 text-center text-red-500 bg-red-50 rounded-lg max-w-md mx-auto my-12 border border-red-100">
        Lỗi: {error}
      </div>
    );
  }

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
      <header className="mb-10 border-b border-[#E5DECE] pb-6">
        <h1 className="font-editorial text-3xl font-bold text-[#1C2421] sm:text-4xl">
          Danh Mục Món Ăn
        </h1>
        <p className="mt-2 text-sm text-[#66726B]">
          Khám phá các công thức nấu ăn theo từng phân loại chuẩn vị.
        </p>
      </header>

      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-6">
        {categories.map((item) => (
          <Link
            href={`/categories/${item.slug}`}
            key={item.id}
            className="group flex flex-col rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-6 shadow-sm transition-all hover:border-[#C6A15B]/60 hover:shadow-md"
          >
            <div className="mb-3 flex items-center justify-between">
              <h2 className="font-editorial text-xl font-bold text-[#1C2421] group-hover:text-[#063C2F] transition-colors">
                {item.name}
              </h2>
              <span className="rounded-full bg-[#E7DED0] px-2.5 py-1 font-mono text-[10px] font-semibold text-[#A8894D]">
                /{item.slug}
              </span>
            </div>
            
            <p className="text-sm leading-relaxed text-[#66726B] line-clamp-3">
              {item.description || "Đang cập nhật mô tả chi tiết cho danh mục này."}
            </p>
            
            <div className="mt-auto pt-4 text-xs font-semibold text-[#063C2F] group-hover:text-[#A8894D] transition-colors">
              Xem công thức →
            </div>
          </Link>
        ))}
      </div>
    </main>
  );
}