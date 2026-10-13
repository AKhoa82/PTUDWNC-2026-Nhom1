
"use client";

import {
  forwardRef,
  useId,
  type InputHTMLAttributes,
  type ReactNode,
} from "react";

export interface InputProps
  extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  helperText?: string;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
}

export const Input = forwardRef<
  HTMLInputElement,
  InputProps
>(function Input(
  {
    label,
    error,
    helperText,
    leftIcon,
    rightIcon,
    className = "",
    id,
    ...props
  },
  ref
) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const messageId = `${inputId}-message`;

  return (
    <div className="w-full">

      {label && (
        <label
          htmlFor={inputId}
          className="mb-2 block text-xs font-semibold uppercase tracking-wider text-[#1C2421]"
        >
          {label}
        </label>
      )}

      <div className="relative">
        {leftIcon && (
          <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#8D958F]">
            {leftIcon}
          </span>
        )}

        <input
          {...props}
          id={inputId}
          ref={ref}
          aria-invalid={Boolean(error)}
          aria-describedby={
            error || helperText ? messageId : undefined
          }
          className={`
            block w-full rounded-lg
            border bg-[#FBF8F1]
            py-2.5 text-sm text-[#1C2421]
            placeholder:text-[#8D958F]
            outline-none transition-all
            focus:ring-2
            disabled:opacity-60
            ${
              error
                ? "border-[#B9674A] focus:ring-[#B9674A]/20"
                : "border-[#DDD4C4] focus:border-[#C6A15B] focus:ring-[#C6A15B]/20"
            }
            ${leftIcon ? "pl-10" : "pl-3.5"}
            ${rightIcon ? "pr-10" : "pr-3.5"}
            ${className}
          `}
        />

        {rightIcon && (
          <span className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-3.5 text-[#8D958F]">
            {rightIcon}
          </span>
        )}
      </div>

      {error ? (
        <p
          id={messageId}
          role="alert"
          className="mt-1.5 text-xs text-[#B9674A]"
        >
          {error}
        </p>
      ) : helperText ? (
        <p
          id={messageId}
          className="mt-1.5 text-xs text-[#8D958F]"
        >
          {helperText}
        </p>
      ) : null}

    </div>
  );
});

Input.displayName = "Input";