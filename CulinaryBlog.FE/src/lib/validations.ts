import { z } from "zod";

export const registerSchema = z
  .object({
    name: z.string().trim().min(2, "Họ và tên phải có ít nhất 2 ký tự."),
    userName: z
      .string()
      .trim()
      .min(3, "Tên định danh phải có ít nhất 3 ký tự.")
      .max(30, "Tên định danh tối đa 30 ký tự.")
      .regex(/^[a-zA-Z0-9._-]+$/, "Tên định danh chứa ký tự không hợp lệ."),
    email: z.string().trim().email("Email không hợp lệ."),
    password: z.string().min(8, "Mật khẩu phải có ít nhất 8 ký tự."),
    confirmPassword: z.string(),
    acceptTerms: z.boolean().refine((value) => value, "Bạn cần đồng ý với điều khoản."),
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ["confirmPassword"],
    message: "Mật khẩu xác nhận không khớp.",
  });

export type RegisterFormData = z.infer<typeof registerSchema>;
