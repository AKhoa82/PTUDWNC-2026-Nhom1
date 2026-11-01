"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";

// 1. Khai báo kiểu dữ liệu để TypeScript không báo lỗi
interface Category {
  id: string;
  name: string;
  slug: string;
  description: string;
}

export default function CategoryManagement() {
  const { data: session } = useSession();
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Form State
  const [isEditing, setIsEditing] = useState(false);
  const [currentId, setCurrentId] = useState("");
  const [formData, setFormData] = useState({ name: "", description: "" });

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

  useEffect(() => {
    fetchCategories();
  }, []);

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
          Authorization: `Bearer ${(session as any)?.accessToken}`, 
        },
        body: JSON.stringify(formData),
      });

      if (!res.ok) throw new Error("Lưu thất bại");
      
      setFormData({ name: "", description: "" });
      setIsEditing(false);
      setCurrentId("");
      fetchCategories();
    } catch (err) {
      alert("Có lỗi xảy ra khi lưu danh mục.");
    }
  };

  const handleEdit = (category: Category) => {
    setIsEditing(true);
    setCurrentId(category.id);
    setFormData({ name: category.name, description: category.description });
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
    } catch (err) {
      alert("Có lỗi xảy ra khi xóa.");
    }
  };

  if (loading) return <div>Đang tải...</div>;

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-6">Quản lý Danh mục</h1>

      {/* Form Tạo/Cập nhật */}
      <form onSubmit={handleSubmit} className="bg-white p-4 rounded shadow mb-8 space-y-4">
        <h2 className="text-lg font-semibold">{isEditing ? "Cập nhật danh mục" : "Tạo danh mục mới"}</h2>
        <div>
          <label className="block text-sm font-medium">Tên danh mục</label>
          <input
            type="text"
            required
            className="w-full border p-2 rounded mt-1"
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
          />
        </div>
        <div>
          <label className="block text-sm font-medium">Mô tả</label>
          <textarea
            className="w-full border p-2 rounded mt-1"
            value={formData.description}
            onChange={(e) => setFormData({ ...formData, description: e.target.value })}
          />
        </div>
        <div className="flex gap-2">
          {/* 2. Đổi component Button thành thẻ button HTML chuẩn */}
          <button 
            type="submit" 
            className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
          >
            {isEditing ? "Cập nhật" : "Tạo mới"}
          </button>
          
          {isEditing && (
            <button 
              type="button" 
              className="border border-gray-300 text-gray-700 px-4 py-2 rounded hover:bg-gray-100"
              onClick={() => { setIsEditing(false); setFormData({ name: "", description: "" }); }}
            >
              Hủy
            </button>
          )}
        </div>
      </form>

      {/* Bảng Danh sách */}
      {error && <div className="text-red-500 mb-4">{error}</div>}
      <table className="w-full bg-white shadow rounded overflow-hidden">
        <thead className="bg-gray-100 text-left">
          <tr>
            <th className="p-4">Tên</th>
            <th className="p-4">Slug</th>
            <th className="p-4">Mô tả</th>
            <th className="p-4 text-right">Thao tác</th>
          </tr>
        </thead>
        <tbody>
          {categories.map((cat) => (
            <tr key={cat.id} className="border-t">
              <td className="p-4">{cat.name}</td>
              <td className="p-4">{cat.slug}</td>
              <td className="p-4">{cat.description}</td>
              <td className="p-4 text-right space-x-2">
                <button type="button" onClick={() => handleEdit(cat)} className="text-blue-600 hover:underline">Sửa</button>
                <button type="button" onClick={() => handleDelete(cat.id)} className="text-red-600 hover:underline">Xóa</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}