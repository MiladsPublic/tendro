import * as React from 'react'
import { cn } from '@/lib/utils'

function Input({ className, type = 'text', ...props }: React.ComponentProps<'input'>) {
  return (
    <input
      type={type}
      className={cn(
        'flex h-12 w-full rounded-2xl border border-[var(--input)] bg-white/80 px-4 py-3 text-base text-[var(--foreground)] shadow-sm outline-none transition placeholder:text-[var(--muted-foreground)] focus-visible:ring-4 focus-visible:ring-[var(--ring)]',
        className,
      )}
      {...props}
    />
  )
}

export { Input }