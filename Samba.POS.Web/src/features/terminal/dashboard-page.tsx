import { ArrowRight, RefreshCw, ShieldCheck, Smartphone, Store } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { useOpenTickets } from '@/features/terminal/use-ticket-queries'
import { usePosStore } from '@/features/terminal/use-pos-store'

const checkpoints = [
  'Terminal login and operator shift handoff',
  'Open ticket list and reopen flow',
  'Menu browse, modifiers, and hold-send pacing',
  'Settlement, refund, and supervised void paths',
]

export function DashboardPage() {
  const operatorName = usePosStore((state) => state.operatorName)
  const status = usePosStore((state) => state.status)
  const pendingQueueCount = usePosStore((state) => state.pendingQueueCount)
  const lastSyncLabel = usePosStore((state) => state.lastSyncLabel)
  const setOperatorName = usePosStore((state) => state.setOperatorName)
  const cycleConnectionState = usePosStore((state) => state.cycleConnectionState)
  const drainQueue = usePosStore((state) => state.drainQueue)
  const openTicketsQuery = useOpenTickets()

  return (
    <div className="space-y-6">
      <section className="grid gap-4 xl:grid-cols-[1.4fr_0.9fr]">
        <Card className="overflow-hidden">
          <CardHeader className="gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <Badge variant="accent">Touch-first shell</Badge>
              <CardTitle className="mt-3 text-4xl">Phase 4 terminal launchpad</CardTitle>
              <CardDescription className="mt-2 max-w-2xl text-base">
                The web rollout now runs through all planned ticket, menu, and settlement slices in the monorepo with live modern API integration.
              </CardDescription>
            </div>
            <Button size="lg" variant="accent">
              Open active shift
              <ArrowRight className="size-4.5" />
            </Button>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-3">
            <div className="rounded-[1.5rem] bg-white/60 p-4">
              <p className="text-sm uppercase tracking-[0.14em] text-[var(--muted-foreground)]">Connection</p>
              <p className="mt-2 text-3xl font-semibold capitalize text-[var(--card-foreground)]">{status}</p>
              <p className="mt-2 text-sm text-[var(--muted-foreground)]">{lastSyncLabel}</p>
            </div>
            <div className="rounded-[1.5rem] bg-white/60 p-4">
              <p className="text-sm uppercase tracking-[0.14em] text-[var(--muted-foreground)]">Queued actions</p>
              <p className="mt-2 text-3xl font-semibold text-[var(--card-foreground)]">{pendingQueueCount}</p>
              <p className="mt-2 text-sm text-[var(--muted-foreground)]">Visible to the operator before settlement.</p>
            </div>
            <div className="rounded-[1.5rem] bg-white/60 p-4">
              <p className="text-sm uppercase tracking-[0.14em] text-[var(--muted-foreground)]">Scope</p>
              <p className="mt-2 text-3xl font-semibold text-[var(--card-foreground)]">4 slices</p>
              <p className="mt-2 text-sm text-[var(--muted-foreground)]">Overview, tickets, menu ordering, and settlement actions are available.</p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-2xl">
              <Store className="size-5" />
              Operator handoff
            </CardTitle>
            <CardDescription>Keep login and station identity visible on shared terminals.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium text-[var(--card-foreground)]" htmlFor="operator-name">
                Active operator
              </label>
              <Input
                id="operator-name"
                value={operatorName}
                onChange={(event) => setOperatorName(event.target.value)}
                placeholder="Enter operator name"
              />
            </div>
            <div className="flex flex-wrap gap-3">
              <Button variant="secondary" onClick={cycleConnectionState}>
                <RefreshCw className="size-4" />
                Cycle connectivity
              </Button>
              <Button variant="outline" onClick={drainQueue}>
                <ShieldCheck className="size-4" />
                Drain local queue
              </Button>
            </div>
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle className="text-2xl">Rollout slices</CardTitle>
            <CardDescription>Value order aligned to the migration plan.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3">
            {checkpoints.map((checkpoint, index) => (
              <div key={checkpoint} className="flex items-center gap-3 rounded-[1.5rem] bg-white/55 px-4 py-3">
                <div className="flex size-10 items-center justify-center rounded-full bg-[var(--primary)] text-sm font-bold text-[var(--primary-foreground)]">
                  {index + 1}
                </div>
                <p className="font-medium text-[var(--card-foreground)]">{checkpoint}</p>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-2xl">
              <Smartphone className="size-5" />
              Device posture
            </CardTitle>
            <CardDescription>Design baseline for tablets and shared counter terminals.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm text-[var(--muted-foreground)]">
            <p>Large controls, one-hand reach zones, and queue visibility are prioritized over dense desktop layouts.</p>
            <p>Canonical truth stays in the modern API. Local state is scoped to UX speed, draft entry, and offline recovery.</p>
            <p>
              {openTicketsQuery.isSuccess
                ? `${openTicketsQuery.data.totalCount} open tickets are currently being served by the modern API.`
                : 'Manual manifest support is in place now. A service worker can be added later once the Vite 8 PWA plugin ecosystem stabilizes.'}
            </p>
          </CardContent>
        </Card>
      </section>
    </div>
  )
}