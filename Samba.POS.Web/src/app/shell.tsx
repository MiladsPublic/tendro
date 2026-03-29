import { NavLink, Outlet } from 'react-router-dom'
import { CloudOff, LayoutGrid, MenuSquare, ReceiptText, Wifi } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { usePosStore } from '@/features/terminal/use-pos-store'

const navigationItems = [
  { to: '/', label: 'Overview', icon: LayoutGrid },
  { to: '/tickets', label: 'Tickets', icon: ReceiptText },
  { to: '/menu', label: 'Menu', icon: MenuSquare },
]

export function AppShell() {
  const stationName = usePosStore((state) => state.stationName)
  const status = usePosStore((state) => state.status)
  const pendingQueueCount = usePosStore((state) => state.pendingQueueCount)

  return (
    <div className="min-h-screen px-4 py-4 sm:px-6 lg:px-8">
      <div className="mx-auto grid min-h-[calc(100vh-2rem)] max-w-7xl gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
        <aside className="glass-card flex flex-col justify-between rounded-[2rem] border border-white/50 p-5">
          <div className="space-y-8">
            <div className="space-y-3">
              <Badge variant="accent">Phase 4 rollout</Badge>
              <div>
                <p className="text-sm uppercase tracking-[0.18em] text-[var(--muted-foreground)]">Station</p>
                <h1 className="mt-2 text-3xl text-[var(--card-foreground)]">{stationName}</h1>
              </div>
              <div className="flex flex-wrap gap-2 text-sm text-[var(--muted-foreground)]">
                <Badge variant={status === 'online' ? 'success' : 'warning'}>
                  <span className="flex items-center gap-1.5">
                    {status === 'online' ? <Wifi className="size-3.5" /> : <CloudOff className="size-3.5" />}
                    {status}
                  </span>
                </Badge>
                <Badge>{pendingQueueCount} queued actions</Badge>
              </div>
            </div>

            <nav className="space-y-2">
              {navigationItems.map(({ to, label, icon: Icon }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={to === '/'}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 rounded-2xl px-4 py-3 text-base font-medium transition',
                      isActive
                        ? 'bg-[var(--primary)] text-[var(--primary-foreground)] shadow-[0_14px_28px_rgba(29,58,52,0.22)]'
                        : 'text-[var(--foreground)] hover:bg-white/60',
                    )
                  }
                >
                  <Icon className="size-5" />
                  <span>{label}</span>
                </NavLink>
              ))}
            </nav>
          </div>

          <div className="rounded-[1.5rem] bg-[rgba(29,58,52,0.08)] p-4 text-sm text-[var(--muted-foreground)]">
            Web POS is configured for monorepo rollout. UI state stays local only for workflow speed and offline recovery.
          </div>
        </aside>

        <main className="glass-card rounded-[2rem] border border-white/50 p-5 sm:p-6 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}