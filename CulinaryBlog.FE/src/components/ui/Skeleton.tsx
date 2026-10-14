
import type { HTMLAttributes } from "react";

interface SkeletonProps
  extends HTMLAttributes<HTMLDivElement> {
  variant?: "text" | "circular" | "rectangular" | "rounded";
  width?: string | number;
  height?: string | number;
}

const variants = {
  text: "h-4 rounded",
  circular: "rounded-full",
  rectangular: "rounded-none",
  rounded: "rounded-xl",
};

export function Skeleton({
  variant = "text",
  width,
  height,
  className = "",
  style,
  ...props
}: SkeletonProps) {
  return (
    <div
      aria-hidden="true"
      className={`
        animate-pulse bg-[#E8E2D5]
        ${variants[variant]}
        ${className}
      `}
      style={{
        width,
        height,
        ...style,
      }}
      {...props}
    />
  );
}

export function RecipeCardSkeleton() {
  return (
    <div className="overflow-hidden rounded-xl border border-[#E5DECE] bg-[#FBF8F1] shadow-sm">

      <Skeleton
        variant="rectangular"
        className="h-48 w-full"
      />

      <div className="space-y-3 p-5">
        <Skeleton width={100} />
        <Skeleton width="80%" height={24} />
        <Skeleton width="100%" />
        <Skeleton width="65%" />

        <div className="flex justify-between border-t border-[#E5DECE] pt-3">
          <Skeleton width={90} />
          <Skeleton width={55} />
        </div>
      </div>
    </div>
  );
}

export function TableRowSkeleton({
  columns = 5,
}: {
  columns?: number;
}) {
  return (
    <tr className="border-b border-[#E5DECE]">
      {Array.from({ length: columns }, (_, index) => (
        <td key={index} className="px-4 py-4">
          <Skeleton className="w-full" />
        </td>
      ))}
    </tr>
  );
}

export function DashboardStatSkeleton() {
  return (
    <div className="space-y-3 rounded-xl border border-[#E5DECE] bg-[#FBF8F1] p-5">
      <Skeleton width={100} />
      <Skeleton width={120} height={32} />
      <Skeleton width={75} />
    </div>
  );
}