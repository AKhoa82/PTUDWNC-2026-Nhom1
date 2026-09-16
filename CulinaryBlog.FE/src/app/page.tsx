"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, type UseFormRegisterReturn } from "react-hook-form";
import { useState } from "react";
import { ArrowLeft, ArrowRight, Check, ChefHat, Eye, EyeOff, Lock, Mail, Sparkles, User, AlertCircle } from "lucide-react";
import { registerSchema, type RegisterFormData } from "../lib/validations";

const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5018/api";

export default function RegisterPage() {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [regSuccess, setRegSuccess] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [serverError, setServerError] = useState("");
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: { name: "", userName: "", email: "", password: "", confirmPassword: "", acceptTerms: true },
  });

  const onSubmit = async (values: RegisterFormData) => {
    setIsSubmitting(true);
    setServerError("");
    setRegSuccess(false);

    try {
      const response = await fetch(`${apiUrl}/auth/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          fullName: values.name,
          username: values.userName,
          email: values.email,
          password: values.password,
          confirmPassword: values.confirmPassword,
          termsAccepted: values.acceptTerms,
        }),
      });
      const data = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
      const validationMessage = data.errors ? Object.values(data.errors).flat()[0] : undefined;

      if (!response.ok) {
        throw new Error(data.message ?? validationMessage ?? "Đăng ký không thành công.");
      }

      setRegSuccess(true);
    } catch (error) {
      setServerError(error instanceof Error ? error.message : "Không thể kết nối đến máy chủ.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const errorMessage = (field: keyof RegisterFormData) => errors[field]?.message;

  return (
    <div className="min-h-screen w-full bg-[#f5f0e6] font-sans selection:bg-[#c6a15b]/25 selection:text-[#022c24]">
      <div className="grid min-h-screen w-full grid-cols-1 bg-[#fbf8f1] lg:grid-cols-12">
        <section className="relative flex min-h-[440px] flex-col justify-between overflow-hidden bg-[#022c24] p-6 text-[#fbf8f1] sm:p-10 lg:col-span-5 lg:min-h-screen lg:p-12">
          <div className="absolute inset-0">
            <img src="https://images.unsplash.com/photo-1556910103-1c02745aae4d?auto=format&fit=crop&w=1400&q=85" alt="Đầu bếp đang chế biến món ăn với thảo mộc tươi" className="h-full w-full scale-105 object-cover object-center transition-transform duration-1000 hover:scale-110" />
            <div className="absolute inset-0 bg-gradient-to-t from-[#022c24]/95 via-[#022c24]/45 to-[#022c24]/30" />
            <div className="absolute inset-0 bg-[#022c24]/20 mix-blend-multiply" />
          </div>

          <div className="relative z-10 flex items-center justify-between gap-4">
            <button type="button" className="group inline-flex items-center gap-2.5 rounded-full border border-[#c6a15b]/40 bg-[#022c24]/85 px-3.5 py-2 text-left backdrop-blur-md" aria-label="Về trang chủ">
              <ChefHat className="h-4 w-4 text-[#c6a15b] transition-transform group-hover:rotate-12" />
              <span><strong className="block text-[11px] leading-none tracking-[0.2em]">CULINARY BLOG</strong><small className="block pt-0.5 text-[8px] font-medium tracking-[0.25em] text-[#c6a15b]">ẨM THỰC VIỆT NAM</small></span>
            </button>
            <span className="hidden items-center gap-1.5 rounded-full border border-[#fbf8f1]/15 bg-[#022c24]/60 px-3 py-1.5 text-[10px] uppercase tracking-[0.2em] text-[#f5f0e6]/70 backdrop-blur-sm sm:inline-flex"><Sparkles className="h-3 w-3 text-[#c6a15b]" /> Gia nhập cộng đồng</span>
          </div>

          <div className="relative z-10 mt-20 space-y-4 lg:mt-auto">
            <div className="h-0.5 w-10 bg-[#c6a15b]" />
            <span className="text-[10px] font-semibold uppercase tracking-[0.25em] text-[#c6a15b] sm:text-[11px]">Cộng đồng tác giả ẩm thực</span>
            <h2 className="max-w-xl text-3xl font-bold leading-[1.12] sm:text-4xl" style={{ fontFamily: "'Playfair Display', serif" }}>“Mỗi công thức <span className="font-normal italic text-[#c6a15b]">đều mang một</span> câu chuyện riêng.”</h2>
            <p className="max-w-md text-xs leading-relaxed text-[#f5f0e6]/80 sm:text-sm">Trở thành người lưu giữ và lan tỏa phong vị ẩm thực Việt. Đóng góp công thức gia truyền, kỹ thuật làm bếp và ghi dấu ấn cá nhân trong không gian biên tập cao cấp.</p>
            <div className="flex items-center justify-between border-t border-[#fbf8f1]/20 pt-3 text-[10px] uppercase tracking-[0.15em] text-[#f5f0e6]/60"><span>Cộng tác viên · Bếp trưởng · Blogger</span><span>Tham gia miễn phí</span></div>
          </div>
        </section>

        <section className="flex min-h-screen flex-col justify-center bg-[#fbf8f1] px-6 py-10 sm:px-12 lg:col-span-7 lg:px-16">
          <div className="mx-auto w-full max-w-[520px] space-y-6">
            <div className="flex items-center justify-between border-b border-[#e5dece]/70 pb-3"><button type="button" className="inline-flex items-center gap-1.5 text-[11px] font-medium uppercase tracking-[0.15em] text-[#8d958f] hover:text-[#063c2f]"><ArrowLeft className="h-3.5 w-3.5 text-[#c6a15b]" /> Trang chủ</button><span className="text-[10px] font-semibold uppercase tracking-[0.2em] text-[#c6a15b]">02 — Đăng ký tác giả</span></div>
            <div className="space-y-2"><div className="h-0.5 w-8 bg-[#c6a15b]" /><h1 className="text-4xl font-bold leading-tight tracking-tight text-[#1c2421] sm:text-5xl" style={{ fontFamily: "'Playfair Display', serif" }}>Đăng ký<br /><span className="font-normal italic text-[#063c2f]">Trở thành một phần của cộng đồng</span></h1><p className="text-sm leading-relaxed text-[#8d958f]">Tạo tài khoản để chia sẻ những công thức và câu chuyện ẩm thực của riêng bạn.</p></div>

            {regSuccess && <div className="flex items-center gap-2.5 rounded-lg border border-[#063c2f]/30 bg-[#063c2f]/10 p-3.5 text-xs font-medium text-[#063c2f]"><Check className="h-4 w-4 shrink-0" /> Đăng ký thành công! Chào mừng bạn đến với cộng đồng.</div>}
            {serverError && <div className="flex items-center gap-2.5 rounded-lg border border-[#b9674a]/30 bg-[#b9674a]/10 p-3.5 text-xs font-medium text-[#8d3c25]" role="alert"><AlertCircle className="h-4 w-4 shrink-0" /> {serverError}</div>}

            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
              <div className="grid gap-3.5 sm:grid-cols-2">
                <FormField label="Họ và tên tác giả *" error={errorMessage("name")} icon={<User className="h-4 w-4" />}><input type="text" placeholder="vd: Lê Anh Khoa" className={fieldClass(Boolean(errors.name))} {...register("name")} /></FormField>
                <FormField label="Tên định danh (Username) *" error={errorMessage("userName")} icon={<User className="h-4 w-4" />}><input type="text" placeholder="vd: khoale_culinary" className={fieldClass(Boolean(errors.userName))} {...register("userName")} /></FormField>
              </div>
              <FormField label="Địa chỉ Email *" error={errorMessage("email")} icon={<Mail className="h-4 w-4" />}><input type="email" placeholder="vd: name@culinaryblog.vn" className={fieldClass(Boolean(errors.email))} {...register("email")} /></FormField>
              <div className="grid gap-3.5 sm:grid-cols-2">
                <PasswordField label="Mật khẩu (tối thiểu 8 ký tự) *" error={errorMessage("password")} visible={showPassword} onToggle={() => setShowPassword(!showPassword)} register={register("password")} />
                <PasswordField label="Xác nhận mật khẩu *" error={errorMessage("confirmPassword")} visible={showConfirmPassword} onToggle={() => setShowConfirmPassword(!showConfirmPassword)} register={register("confirmPassword")} />
              </div>
              <label className="flex cursor-pointer items-start gap-2.5 text-xs text-[#1c2421]"><input type="checkbox" className="mt-0.5 h-4 w-4 accent-[#063c2f]" {...register("acceptTerms")} /><span className="leading-relaxed font-medium text-[#1c2421]/80">Tôi đồng ý với Quy chuẩn biên soạn &amp; Điều khoản bảo vệ tác quyền ẩm thực của Culinary Blog</span></label>
              {errors.acceptTerms && <p className="text-xs font-medium text-[#b9674a]">{String(errors.acceptTerms.message)}</p>}
              <button type="submit" disabled={isSubmitting} className="mt-3 flex h-[52px] w-full items-center justify-center gap-2 rounded-lg bg-[#c6a15b] text-xs font-semibold uppercase tracking-wider text-[#022c24] shadow-sm transition-all hover:bg-[#063c2f] hover:text-[#fbf8f1] disabled:cursor-not-allowed disabled:opacity-50">{isSubmitting ? <span className="h-4 w-4 animate-spin rounded-full border-2 border-[#022c24] border-t-transparent" /> : <>Đăng ký tài khoản tác giả <ArrowRight className="h-4 w-4" /></>}</button>
            </form>
            <div className="border-t border-[#e5dece] pt-4 text-center text-xs text-[#8d958f]"><span>Đã có tài khoản tác giả? </span><button type="button" className="ml-1 font-bold text-[#063c2f] underline underline-offset-4 hover:text-[#c6a15b]">Đăng nhập vào hệ thống →</button><p className="pt-2 text-[11px] text-[#8d958f]/70">Không gian chia sẻ tinh hoa ẩm thực ba miền và tri thức làm bếp Việt</p></div>
          </div>
        </section>
      </div>
    </div>
  );
}

function fieldClass(hasError: boolean) {
  return `block h-[52px] w-full rounded-lg border bg-[#f5f0e6]/50 px-3.5 text-xs text-[#1c2421] outline-none placeholder:text-[#8d958f] transition-colors focus:bg-[#fbf8f1] sm:text-sm ${hasError ? "border-[#b9674a] focus:border-[#b9674a]" : "border-[#e5dece] focus:border-[#c6a15b]"}`;
}

function FormField({ label, error, icon, children }: { label: string; error?: string; icon: React.ReactNode; children: React.ReactNode }) {
  return <div className="space-y-1.5"><label className="block text-[11px] font-semibold uppercase tracking-[0.15em] text-[#063c2f]">{label}</label><div className="relative"><div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#c6a15b]">{icon}</div>{children}</div>{error && <p className="text-xs font-medium text-[#b9674a]">{error}</p>}</div>;
}

function PasswordField({ label, error, visible, onToggle, register }: { label: string; error?: string; visible: boolean; onToggle: () => void; register: UseFormRegisterReturn }) {
  return <FormField label={label} error={error} icon={<Lock className="h-4 w-4" />}><input type={visible ? "text" : "password"} placeholder="••••••••" className={`${fieldClass(Boolean(error))} pr-10`} {...register} /><button type="button" onClick={onToggle} className="absolute inset-y-0 right-0 flex items-center pr-3.5 text-[#8d958f] hover:text-[#063c2f]" aria-label={visible ? "Ẩn mật khẩu" : "Hiện mật khẩu"}>{visible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}</button></FormField>;
}
