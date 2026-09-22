"use client";

import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowLeft, ArrowRight, AlertCircle, ChefHat, Eye, EyeOff, Lock, Mail } from "lucide-react";
import { useState } from "react";
import { z } from "zod";

const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api";

const loginSchema = z.object({
  email: z.string().trim().email("Email không hợp lệ."),
  password: z.string().min(8, "Mật khẩu phải có ít nhất 8 ký tự."),
});

type LoginFormData = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const [showPassword, setShowPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [serverError, setServerError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");
  const { register, handleSubmit, formState: { errors } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  });

  const onSubmit = async (values: LoginFormData) => {
    setIsSubmitting(true);
    setServerError("");
    setSuccessMessage("");

    try {
      const response = await fetch(`${apiUrl}/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const responseText = await response.text();
      let data: { message?: string } = {};

      if (responseText.trim()) {
        try {
          data = JSON.parse(responseText) as { message?: string };
        } catch {
          data = {};
        }
      }

      if (!response.ok) {
        const message = response.status === 401
          ? "Email hoặc mật khẩu không đúng."
          : response.status === 503
            ? "Cơ sở dữ liệu đang tạm thời không khả dụng. Vui lòng thử lại sau."
            : data.message ?? "Đăng nhập không thành công. Vui lòng thử lại.";

        throw new Error(message);
      }

      setSuccessMessage(data.message ?? "Đăng nhập thành công.");
    } catch (error) {
      setServerError(error instanceof Error ? error.message : "Không thể kết nối đến máy chủ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="min-h-screen bg-[#f5f0e6] lg:grid lg:grid-cols-12">
      <section className="relative hidden min-h-screen overflow-hidden bg-[#022c24] p-12 text-[#fbf8f1] lg:col-span-5 lg:flex lg:flex-col lg:justify-between">
        <div className="absolute inset-0">
          <img src="https://images.unsplash.com/photo-1556910103-1c02745aae4d?auto=format&fit=crop&w=1400&q=85" alt="Đầu bếp đang chế biến món ăn" className="h-full w-full object-cover" />
          <div className="absolute inset-0 bg-gradient-to-t from-[#022c24]/95 via-[#022c24]/55 to-[#022c24]/35" />
        </div>
        <div className="relative z-10 inline-flex w-fit items-center gap-2 rounded-full border border-[#c6a15b]/40 bg-[#022c24]/85 px-4 py-2 text-xs font-bold tracking-[0.18em]"><ChefHat className="h-4 w-4 text-[#c6a15b]" /> CULINARY BLOG</div>
        <div className="relative z-10 max-w-xl"><div className="mb-5 h-0.5 w-10 bg-[#c6a15b]" /><p className="mb-3 text-xs font-semibold uppercase tracking-[0.2em] text-[#c6a15b]">Không gian tác giả ẩm thực</p><h1 className="text-5xl font-bold leading-tight" style={{ fontFamily: "'Playfair Display', serif" }}>Chào mừng bạn trở lại căn bếp.</h1><p className="mt-6 leading-7 text-[#f5f0e6]/80">Tiếp tục lưu giữ, chia sẻ và lan tỏa những câu chuyện ngon lành của bạn.</p></div>
      </section>

      <section className="flex min-h-screen items-center justify-center px-6 py-10 sm:px-12 lg:col-span-7 lg:px-20">
        <div className="w-full max-w-[500px]">
          <div className="mb-8 flex items-center justify-between border-b border-[#e5dece] pb-3 text-[11px] font-medium uppercase tracking-[0.15em] text-[#8d958f]"><Link href="/" className="inline-flex items-center gap-1.5 hover:text-[#063c2f]"><ArrowLeft className="h-3.5 w-3.5 text-[#c6a15b]" /> Trang chủ</Link><span className="text-[#c6a15b]">Đăng nhập tác giả</span></div>
          <div className="mb-8"><div className="mb-4 h-0.5 w-8 bg-[#c6a15b]" /><h2 className="text-5xl font-bold leading-tight text-[#1c2421]" style={{ fontFamily: "'Playfair Display', serif" }}>Đăng nhập<br /><span className="font-normal italic text-[#063c2f]">Vào không gian của bạn</span></h2><p className="mt-5 leading-7 text-[#8d958f]">Đăng nhập để tiếp tục quản lý công thức và hồ sơ tác giả.</p></div>

          {serverError && <div className="mb-5 flex items-center gap-2 rounded-lg border border-[#b9674a]/30 bg-[#b9674a]/10 p-3.5 text-sm text-[#8d3c25]" role="alert"><AlertCircle className="h-4 w-4 shrink-0" /> {serverError}</div>}
          {successMessage && <div className="mb-5 rounded-lg border border-[#063c2f]/30 bg-[#063c2f]/10 p-3.5 text-sm text-[#063c2f]" role="status">{successMessage}</div>}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
            <label className="block"><span className="mb-2 block text-[11px] font-semibold uppercase tracking-[0.15em] text-[#063c2f]">Địa chỉ Email *</span><span className="relative block"><Mail className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#c6a15b]" /><input type="email" placeholder="vd: name@culinaryblog.vn" className={`${fieldClass(Boolean(errors.email))} pl-10`} {...register("email")} /></span>{errors.email && <span className="mt-1 block text-xs text-[#b9674a]">{errors.email.message}</span>}</label>
            <label className="block"><span className="mb-2 block text-[11px] font-semibold uppercase tracking-[0.15em] text-[#063c2f]">Mật khẩu *</span><span className="relative block"><Lock className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#c6a15b]" /><input type={showPassword ? "text" : "password"} placeholder="••••••••" className={`${fieldClass(Boolean(errors.password))} pl-10 pr-11`} {...register("password")} /><button type="button" onClick={() => setShowPassword(!showPassword)} className="absolute right-3.5 top-1/2 -translate-y-1/2 text-[#8d958f]" aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}>{showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}</button></span>{errors.password && <span className="mt-1 block text-xs text-[#b9674a]">{errors.password.message}</span>}</label>
            <button type="submit" disabled={isSubmitting} className="flex h-[52px] w-full items-center justify-center gap-2 rounded-lg bg-[#c6a15b] text-sm font-semibold uppercase tracking-wider text-[#022c24] transition hover:bg-[#063c2f] hover:text-[#fbf8f1] disabled:opacity-50">{isSubmitting ? "Đang đăng nhập..." : <>Đăng nhập <ArrowRight className="h-4 w-4" /></>}</button>
          </form>
          <p className="mt-7 border-t border-[#e5dece] pt-5 text-center text-sm text-[#8d958f]">Chưa có tài khoản? <Link href="/" className="font-bold text-[#063c2f] underline underline-offset-4">Đăng ký ngay →</Link></p>
        </div>
      </section>
    </main>
  );
}

function fieldClass(hasError: boolean) {
  return `h-[52px] w-full rounded-lg border bg-[#f5f0e6]/50 px-3.5 text-sm text-[#1c2421] outline-none placeholder:text-[#8d958f] focus:bg-[#fbf8f1] ${hasError ? "border-[#b9674a]" : "border-[#e5dece] focus:border-[#c6a15b]"}`;
}