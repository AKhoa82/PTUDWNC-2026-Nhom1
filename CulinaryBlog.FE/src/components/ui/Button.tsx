
"use client";

import type {
  ButtonHTMLAttributes,
  ReactNode,
} from "react";
import { Loader2 } from "lucide-react";

interface ButtonProps
  extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?:
    | "primary"
    | "secondary"
    | "outline"
    | "ghost"
    | "danger";
  size?: "sm" | "md" | "lg";
  isLoading?: boolean;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
}

const variants = {
  primary:
    "bg-[#C6A15B] text-[#022C24] hover:bg-[#B8934E]",
  secondary:
    "bg-[#063C2F] text-white hover:bg-[#022C24]",
  outline:
    "border border-[#C6A15B]/50 bg-[#FBF8F1] text-[#063C2F] hover:bg-[#F5F0E6]",
  ghost:
    "text-[#1C2421] hover:bg-[#F5F0E6]",
  danger:
    "bg-[#B9674A] text-white hover:bg-[#A5573C]",
};

const sizes = {
  sm: "px-3 py-1.5 text-xs gap-1.5",
  md: "px-4 py-2 text-sm gap-2",
  lg: "px-6 py-3 text-base gap-2.5",
};

export function Button({
  variant = "primary",
  size = "md",
  isLoading = false,
  leftIcon,
  rightIcon,
  disabled,
  className = "",
  children,
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || isLoading}
      aria-busy={isLoading || undefined}
      className={`
        inline-flex cursor-pointer items-center justify-center
        rounded-lg font-semibold shadow-sm
        transition-all duration-200
        focus-visible:outline-none
        focus-visible:ring-2
        focus-visible:ring-[#C6A15B]
        disabled:cursor-not-allowed
        disabled:opacity-50
        ${variants[variant]}
        ${sizes[size]}
        ${className}
      `}
      {...props}
    >
      {isLoading ? (
        <Loader2 className="h-4 w-4 animate-spin" />
      ) : (
        leftIcon
      )}

      <span>{children}</span>

      {!isLoading && rightIcon}
    </button>
  );
}