
import type { HTMLAttributes } from "react";

interface BadgeProps
  extends HTMLAttributes<HTMLSpanElement> {
  variant?:
    | "default"
    | "success"
    | "warning"
    | "danger"
    | "info"
    | "outline";
  size?: "sm" | "md";
}

const variants = {
  default:
    "bg-[#F5F0E6] text-[#1C2421] border-[#E5DECE]",
  success:
    "bg-[#063C2F]/10 text-[#063C2F] border-[#063C2F]/20",
  warning:
    "bg-[#C6A15B]/15 text-[#A8894D] border-[#C6A15B]/30",
  danger:
    "bg-[#B9674A]/15 text-[#B9674A] border-[#B9674A]/30",
  info:
    "bg-[#062E27]/10 text-[#063C2F] border-[#062E27]/20",
  outline:
    "bg-transparent text-[#A8894D] border-[#C6A15B]/40",
};

export function Badge({
  variant = "default",
  size = "md",
  className = "",
  children,
  ...props
}: BadgeProps) {
  return (
    <span
      className={`
        inline-flex items-center
        whitespace-nowrap rounded-full
        border font-medium
        ${variants[variant]}
        ${
          size === "sm"
            ? "px-2 py-0.5 text-xs"
            : "px-2.5 py-1 text-xs"
        }
        ${className}
      `}
      {...props}
    >
      {children}
    </span>
  );
}