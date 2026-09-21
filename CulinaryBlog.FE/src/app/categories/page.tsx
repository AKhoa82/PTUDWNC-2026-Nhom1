'use client';

import { useEffect, useState } from 'react';

interface Category {
  id: string;
  name: string;
  slug: string;
  description?: string;
  createdAt?: string;
}

export default function CategoriesPage() {
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('http://localhost:5018/api/categories')
      .then((res) => {
        if (!res.ok) throw new Error('Không thể tải danh sách danh mục');
        return res.json();
      })
      .then((data: Category[]) => {
        setCategories(data);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message);
        setLoading(false);
      });
  }, []);

  if (loading) {
    return (
      <div className="flex justify-center items-center min-h-[300px]">
        <p className="text-gray-500 font-medium">Đang tải danh mục món ăn...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4 text-center text-red-500 bg-red-50 rounded-lg max-w-md mx-auto my-8">
        Lỗi: {error}
      </div>
    );
  }

  return (
    <div className="max-w-5xl mx-auto p-6">
      <h1 className="text-3xl font-bold mb-6 text-gray-800 border-b pb-3">
        Danh Mục Món Ăn
      </h1>

      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-6">
        {categories.map((item) => (
          <div
            key={item.id}
            className="p-5 border border-gray-200 rounded-xl shadow-sm hover:shadow-md transition-all bg-white"
          >
            <h2 className="text-xl font-semibold text-emerald-600 mb-1">
              {item.name}
            </h2>
            <span className="inline-block bg-emerald-50 text-emerald-700 text-xs px-2.5 py-1 rounded-md font-mono mb-3">
              /{item.slug}
            </span>
            <p className="text-gray-600 text-sm leading-relaxed">
              {item.description || 'Chưa có mô tả.'}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}