import { useMemo } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'
import { menuCatalog } from '@/features/terminal/menu-catalog'
import { useAddOrder, useCreateTicket, useOpenTickets } from '@/features/terminal/use-ticket-queries'
import { usePosStore } from '@/features/terminal/use-pos-store'

export function MenuPage() {
  const categories = usePosStore((state) => state.categories)
  const selectedCategory = usePosStore((state) => state.selectedCategory)
  const setSelectedCategory = usePosStore((state) => state.setSelectedCategory)
  const selectedTicketId = usePosStore((state) => state.selectedTicketId)
  const openTicketsQuery = useOpenTickets()
  const createTicketMutation = useCreateTicket()
  const addOrderMutation = useAddOrder()
  const menuItems = useMemo(
    () => menuCatalog.filter((item) => item.category === selectedCategory),
    [selectedCategory],
  )

  async function handleAddToTicket(menuItemId: number) {
    let ticketId = selectedTicketId

    if (!ticketId) {
      const ticket = await createTicketMutation.mutateAsync()
      ticketId = ticket.id
    }

    await addOrderMutation.mutateAsync({
      ticketId,
      payload: {
        menuItemId,
        quantity: 1,
      },
    })
  }

  return (
    <div className="space-y-6">
      <div>
        <Badge variant="accent">Ordering slice</Badge>
        <h2 className="mt-3 text-4xl text-[var(--card-foreground)]">Menu browse and modifier runway</h2>
        <p className="mt-2 max-w-2xl text-[var(--muted-foreground)]">
          Categories and touch targets now post order lines to the modern tickets endpoint. Menu catalog data remains local until menu endpoints exist.
        </p>
      </div>

      <div className="grid gap-4 xl:grid-cols-[290px_1fr]">
        <Card>
          <CardHeader>
            <CardTitle className="text-2xl">Categories</CardTitle>
            <CardDescription>Optimized for fast thumb navigation on shared tablets.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3">
            {categories.map((category) => (
              <Button
                key={category}
                variant={category === selectedCategory ? 'accent' : 'outline'}
                size="lg"
                className="justify-start"
                onClick={() => setSelectedCategory(category)}
              >
                {category}
              </Button>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-2xl">{selectedCategory}</CardTitle>
            <CardDescription>
              {openTicketsQuery.data?.items.length
                ? `Adding items will use ticket ${selectedTicketId ?? openTicketsQuery.data.items[0].id}.`
                : 'A ticket will be created automatically the first time an item is added.'}
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            {addOrderMutation.isError ? (
              <div className="rounded-[1.5rem] bg-amber-50 p-4 text-sm text-amber-900 md:col-span-2">
                {(addOrderMutation.error as Error).message}
              </div>
            ) : null}
            {menuItems.map((item) => {
              const Icon = item.icon

              return (
                <div key={item.name} className={cn('rounded-[1.8rem] bg-white/60 p-5 shadow-[0_18px_35px_rgba(24,46,41,0.08)]')}>
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <Icon className="size-4.5 text-[var(--accent)]" />
                        <h3 className="text-xl text-[var(--card-foreground)]">{item.name}</h3>
                      </div>
                      <p className="mt-2 text-sm text-[var(--muted-foreground)]">{item.note}</p>
                    </div>
                    <Badge>${item.price.toFixed(2)}</Badge>
                  </div>
                  <div className="mt-5 flex gap-3">
                    <Button className="flex-1" onClick={() => void handleAddToTicket(item.id)} disabled={addOrderMutation.isPending || createTicketMutation.isPending}>
                      {addOrderMutation.isPending || createTicketMutation.isPending ? 'Sending...' : 'Add to ticket'}
                    </Button>
                    <Button className="flex-1" variant="secondary">
                      Modifiers
                    </Button>
                  </div>
                </div>
              )
            })}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}