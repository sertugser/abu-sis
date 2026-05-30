/* eslint-disable react-refresh/only-export-components */
import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"

const badgeVariants = cva(
  "inline-flex items-center rounded-md border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
  {
    variants: {
      variant: {
        default: "border-transparent bg-gray-800 text-white",
        secondary: "border border-gray-300 bg-gray-100 text-gray-700",
        destructive: "border-transparent bg-red-600 text-white",
        outline: "border border-gray-300 bg-white text-gray-700",
        success: "border-transparent bg-green-600 text-white",
        warning: "border-transparent bg-amber-500 text-white",
        info: "border-transparent bg-blue-600 text-white",
        accent: "border-transparent bg-teal-600 text-white",
        // Soft (açık zemin + koyu yazı) — modern, göz yormayan durum rozetleri
        successSoft: "border border-green-200 bg-green-50 text-green-700",
        warningSoft: "border border-amber-200 bg-amber-50 text-amber-700",
        infoSoft: "border border-blue-200 bg-blue-50 text-blue-700",
        dangerSoft: "border border-red-200 bg-red-50 text-red-700",
        neutralSoft: "border border-border bg-muted text-muted-foreground",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />
}

export { Badge, badgeVariants }