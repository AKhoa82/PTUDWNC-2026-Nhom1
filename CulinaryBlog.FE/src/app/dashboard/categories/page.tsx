"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";

// Bổ sung recipeCount theo yêu cầu FR-CAT-001
interface Category {
  id: string;
  name: string;
  slug: string;
  description: string;
  recipeCount?: number; 
}

export default function CategoryManagement() {
  const { data: session, status } = useSession();
  const router = useRouter();
  
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Bổ sung trường slug vào Form State
  const [isEditing, setIsEditing] = useState(false);
  const [currentId, setCurrentId] = useState("");
  const [formData, setFormData] = useState({ name: "", slug: "", description: "" });

  // 1. Phân quyền giao diện: Đẩy người dùng không phải Admin ra khỏi trang (FR-CAT-003/004/005)
  useEffect(() => {
    if (status === "loading") return;
    const userRole = (session?.user as any)?.role;
    if (!session || userRole !== "Admin") {
      router.push("/dashboard"); 
    } else {
      fetchCategories();
    }
  }, [session, status, router]);

  const fetchCategories = async () => {
    try {
      const res = await fetch("http://localhost:5018/api/v1/categories");
      const data = await res.json();
      setCategories(data);
    } catch (err) {
      setError("Không thể tải danh sách danh mục");
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const url = isEditing 
      ? `http://localhost:5018/api/v1/categories/${currentId}`
      : `http://localhost:5018/api/v1/categories`;
    const method = isEditing ? "PUT" : "POST";

    try {
      const res = await fetch(url, {
        method,
        headers: {
          "Content-Type": "application/json",
          // 2. Gửi kèm Token hợp lệ để tránh lỗi 401 Unauthorized
          Authorization: `Bearer ${(session as any)?.accessToken}`, 
        },
        body: JSON.stringify(formData),
      });

      if (!res.ok) throw new Error("Lưu thất bại. Vui lòng kiểm tra lại quyền hoặc dữ liệu.");
      
      setFormData({ name: "", slug: "", description: "" });
      setIsEditing(false);
      setCurrentId("");
      fetchCategories();
      alert(isEditing ? "Cập nhật thành công!" : "Tạo mới thành công!");
    } catch (err: any) {
      alert(err.message);
    }
  };

  const handleEdit = (category: Category) => {
    setIsEditing(true);
    setCurrentId(category.id);
    setFormData({ name: category.name, slug: category.slug, description: category.description });
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Bạn có chắc chắn muốn xóa danh mục này?")) return;

    try {
      const res = await fetch(`http://localhost:5018/api/v1/categories/${id}`, {
        method: "DELETE",
        headers: { 
          Authorization: `Bearer ${(session as any)?.accessToken}` 
        },
      });

      if (res.status === 409) {
        alert("Không thể xóa danh mục đang chứa công thức.");
        return;
      }
      
      if (!res.ok) throw new Error("Xóa thất bại");
      fetchCategories();
      alert("Đã xóa danh mục thành công.");
    } catch (err) {
      alert("Có lỗi xảy ra khi xóa.");
    }
  };

  if (status === "loading" || loading) return <div className="p-6">Đang kiểm tra quyền truy cập...</div>;

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-6">Quản lý Danh mục</h1>

      {/* Form Tạo/Cập nhật */}
      <form onSubmit={handleSubmit} className="bg-white p-6 rounded shadow mb-8 space-y-4 border border-gray-100">
        <h2 className="text-lg font-semibold text-gray-800">{isEditing ? "Cập nhật danh mục" : "Tạo danh mục mới"}</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Tên danh mục</label>
            <input
              type="text"
              required
              className="w-full border border-gray-300 p-2 rounded mt-1 focus:ring-[#063C2F] focus:border-[#063C2F]"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Slug (Đường dẫn tĩnh)</label>
            <input
              type="text"
              required
              className="w-full border border-gray-300 p-2 rounded mt-1 focus:ring-[#063C2F] focus:border-[#063C2F]"
              value={formData.slug}
              onChange={(e) => setFormData({ ...formData, slug: e.target.value })}
              placeholder="VD: mon-viet"
            />
          </div>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700">Mô tả</label>
          <textarea
            className="w-full border border-gray-300 p-2 rounded mt-1 focus:ring-[#063C2F] focus:border-[#063C2F]"
            rows={3}
            value={formData.description}
            onChange={(e) => setFormData({ ...formData, description: e.target.value })}
          />
        </div>
        <div className="flex gap-3 pt-2">
          <button 
            type="submit" 
            className="bg-[#063C2F] text-white px-5 py-2 rounded font-medium hover:bg-[#04281f] transition-colors"
          >
            {isEditing ? "Cập nhật" : "Tạo mới"}
          </button>
          
          {isEditing && (
            <button 
              type="button" 
              className="bg-gray-100 text-gray-700 px-5 py-2 rounded font-medium hover:bg-gray-200 transition-colors"
              onClick={() => { setIsEditing(false); setFormData({ name: "", slug: "", description: "" }); }}
            >
              Hủy
            </button>
          )}
        </div>
      </form>

      {/* Bảng Danh sách */}
      {error && <div className="text-red-500 mb-4 bg-red-50 p-3 rounded">{error}</div>}
      <div className="overflow-x-auto bg-white shadow rounded border border-gray-100">
        <table className="w-full text-sm text-left text-gray-600">
          <thead className="bg-gray-50 text-gray-700 uppercase font-semibold border-b">
            <tr>
              <th className="px-6 py-4">Tên</th>
              <th className="px-6 py-4">Slug</th>
              <th className="px-6 py-4">Mô tả</th>
              {/* 3. Hiển thị số lượng công thức theo FR-CAT-001 */}
              <th className="px-6 py-4 text-center">Số công thức</th>
              <th className="px-6 py-4 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {categories.map((cat) => (
              <tr key={cat.id} className="border-b hover:bg-gray-50">
                <td className="px-6 py-4 font-medium text-gray-900">{cat.name}</td>
                <td className="px-6 py-4 font-mono text-xs">{cat.slug}</td>
                <td className="px-6 py-4 truncate max-w-xs">{cat.description}</td>
                <td className="px-6 py-4 text-center">
                  <span className="bg-[#E7DED0] text-[#A8894D] px-2 py-1 rounded-full text-xs font-bold">
                    {cat.recipeCount || 0}
                  </span>
                </td>
                <td className="px-6 py-4 text-right space-x-4">
                  <button type="button" onClick={() => handleEdit(cat)} className="font-medium text-blue-600 hover:text-blue-800">Sửa</button>
                  <button type="button" onClick={() => handleDelete(cat.id)} className="font-medium text-red-600 hover:text-red-800">Xóa</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}