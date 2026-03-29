import type * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center rounded-full border px-3 py-1 text-xs font-semibold tracking-[0.12em] uppercase',
  {
    variants: {
      variant: {
        default: 'border-white/60 bg-white/65 text-[var(--foreground)]',
        success: 'border-emerald-200 bg-emerald-100 text-emerald-900',
        warning: 'border-amber-200 bg-amber-100 text-amber-900',
        accent: 'border-[rgba(208,111,69,0.32)] bg-[rgba(208,111,69,0.16)] text-[var(--accent)]',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
)

function Badge({ className, variant, ...props }: React.ComponentProps<'div'> & VariantProps<typeof badgeVariants>) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />
}

export { Badge, badgeVariants }