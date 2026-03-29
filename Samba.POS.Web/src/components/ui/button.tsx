import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-2xl text-sm font-semibold transition-all disabled:pointer-events-none disabled:opacity-50 outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)] active:scale-[0.99]',
  {
    variants: {
      variant: {
        default: 'bg-[var(--primary)] text-[var(--primary-foreground)] shadow-[0_12px_24px_rgba(29,58,52,0.22)] hover:bg-[#24463f]',
        secondary: 'bg-[var(--secondary)] text-[var(--secondary-foreground)] hover:bg-[#d7c2a2]',
        outline: 'border border-[var(--border)] bg-white/50 text-[var(--foreground)] hover:bg-white/80',
        ghost: 'text-[var(--foreground)] hover:bg-white/55',
        accent: 'bg-[var(--accent)] text-[var(--accent-foreground)] shadow-[0_14px_28px_rgba(208,111,69,0.28)] hover:bg-[#c46036]',
      },
      size: {
        default: 'h-11 px-5 py-2.5',
        sm: 'h-9 rounded-xl px-3.5',
        lg: 'h-13 rounded-3xl px-6 text-base',
        icon: 'h-11 w-11',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
)

type ButtonProps = React.ComponentProps<'button'> &
  VariantProps<typeof buttonVariants> & {
    asChild?: boolean
  }

function Button({ className, variant, size, asChild = false, ...props }: ButtonProps) {
  const Comp = asChild ? Slot : 'button'

  return <Comp className={cn(buttonVariants({ variant, size, className }))} {...props} />
}

export { Button, buttonVariants }