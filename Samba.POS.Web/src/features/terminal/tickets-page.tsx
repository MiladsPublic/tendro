import { useEffect, useMemo, useState } from 'react'
import { Clock3, ReceiptText } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import { toTicketSummary } from '@/features/terminal/ticket-mappers'
import {
  useCloseTicket,
  useCreateTicket,
  useOpenTickets,
  useProcessPayment,
  useRefundPayment,
  useReprintTicket,
  useSelectedTicket,
  useTicketPayments,
  useUpdateOrderState,
  useUpdateTicketState,
  useVoidOrder,
} from '@/features/terminal/use-ticket-queries'
import { usePosStore } from '@/features/terminal/use-pos-store'

export function TicketsPage() {
  const [paymentAmount, setPaymentAmount] = useState('')
  const [ticketState, setTicketState] = useState('Ready')
  const [lastActionMessage, setLastActionMessage] = useState<string | null>(null)
  const selectedTicketId = usePosStore((state) => state.selectedTicketId)
  const setSelectedTicketId = usePosStore((state) => state.setSelectedTicketId)
  const ticketsQuery = useOpenTickets()
  const createTicketMutation = useCreateTicket()
  const selectedTicketQuery = useSelectedTicket(selectedTicketId)
  const ticketPaymentsQuery = useTicketPayments(selectedTicketId)
  const processPaymentMutation = useProcessPayment()
  const refundPaymentMutation = useRefundPayment()
  const closeTicketMutation = useCloseTicket()
  const reprintTicketMutation = useReprintTicket()
  const updateTicketStateMutation = useUpdateTicketState()
  const updateOrderStateMutation = useUpdateOrderState()
  const voidOrderMutation = useVoidOrder()
  const tickets = ticketsQuery.data?.items.map(toTicketSummary) ?? []
  const selectedTicket = selectedTicketQuery.data
  const numericAmount = Number(paymentAmount)
  const resolvedPaymentAmount = useMemo(() => {
    if (!selectedTicket) {
      return 0
    }

    if (!Number.isFinite(numericAmount) || numericAmount <= 0) {
      return selectedTicket.remainingAmount
    }

    return numericAmount
  }, [numericAmount, selectedTicket])

  useEffect(() => {
    if (!selectedTicketId && tickets.length > 0) {
      setSelectedTicketId(tickets[0].id)
    }
  }, [selectedTicketId, setSelectedTicketId, tickets])

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <Badge>Open workflow</Badge>
          <h2 className="mt-3 text-4xl text-[var(--card-foreground)]">Ticket board</h2>
          <p className="mt-2 max-w-2xl text-[var(--muted-foreground)]">
            This slice is ready for API integration with open ticket queries, reopen actions, and close-state transitions.
          </p>
        </div>
        <Button size="lg" onClick={() => createTicketMutation.mutate()} disabled={createTicketMutation.isPending}>
          {createTicketMutation.isPending ? 'Creating ticket...' : 'Create walk-in ticket'}
        </Button>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-2xl">
              <ReceiptText className="size-5" />
              Active tickets
            </CardTitle>
            <CardDescription>
              {ticketsQuery.isPending
                ? 'Loading open tickets from the modern API.'
                : 'Touch-sized rows leave room for rapid reopen and settlement decisions.'}
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3">
            {ticketsQuery.isError ? (
              <div className="rounded-[1.5rem] bg-amber-50 p-4 text-sm text-amber-900">
                {(ticketsQuery.error as Error).message}
              </div>
            ) : null}
            {!ticketsQuery.isPending && tickets.length === 0 ? (
              <div className="rounded-[1.5rem] bg-white/60 p-4 text-sm text-[var(--muted-foreground)]">
                No open tickets returned yet. Create one to start the ordering flow.
              </div>
            ) : null}
            {tickets.map((ticket) => (
              <button
                key={ticket.id}
                type="button"
                onClick={() => setSelectedTicketId(ticket.id)}
                className={cn(
                  'grid gap-3 rounded-[1.6rem] border border-transparent bg-white/55 p-4 text-left transition hover:bg-white/80 sm:grid-cols-[1fr_auto] sm:items-center',
                  ticket.id === selectedTicketId && 'border-[var(--accent)] bg-white shadow-[0_18px_40px_rgba(208,111,69,0.16)]',
                )}
              >
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-lg font-semibold text-[var(--card-foreground)]">{ticket.label}</p>
                    <Badge variant={ticket.state === 'open' ? 'success' : 'warning'}>{ticket.state}</Badge>
                  </div>
                  <p className="mt-1 text-sm text-[var(--muted-foreground)]">{ticket.table} • {ticket.courses} active courses</p>
                </div>
                <div className="text-right">
                  <p className="text-2xl font-semibold text-[var(--card-foreground)]">${ticket.total.toFixed(2)}</p>
                  <p className="text-sm text-[var(--muted-foreground)]">Tap to resume</p>
                </div>
              </button>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-2xl">
              <Clock3 className="size-5" />
              Selected ticket
            </CardTitle>
            <CardDescription>Detail payload is loaded from the ticket endpoint with operator actions for settlement and supervision.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {selectedTicketQuery.isPending ? (
              <div className="rounded-[1.5rem] bg-white/60 p-4 text-sm text-[var(--muted-foreground)]">Loading selected ticket...</div>
            ) : null}
            {selectedTicket ? (
              <>
                <div className="rounded-[1.5rem] bg-white/60 p-4 text-sm text-[var(--muted-foreground)]">
                  Ticket total: ${selectedTicket.totalAmount.toFixed(2)}. Remaining: ${selectedTicket.remainingAmount.toFixed(2)}.
                </div>
                <div className="rounded-[1.5rem] bg-white/60 p-4 text-sm text-[var(--muted-foreground)]">
                  {selectedTicket.orders.length} orders and {selectedTicket.payments.length} payments are currently attached.
                </div>
                <div className="grid gap-2 rounded-[1.5rem] bg-white/60 p-4">
                  <p className="text-sm text-[var(--muted-foreground)]">Ticket actions</p>
                  <div className="flex flex-wrap gap-2">
                    <Input value={ticketState} onChange={(event) => setTicketState(event.target.value)} placeholder="Ticket state" />
                    <Button
                      variant="secondary"
                      onClick={() =>
                        updateTicketStateMutation.mutate({
                          ticketId: selectedTicket.id,
                          payload: {
                            stateName: 'Kitchen Status',
                            stateValue: ticketState,
                          },
                        })
                      }
                      disabled={updateTicketStateMutation.isPending}
                    >
                      Update state
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => closeTicketMutation.mutate(selectedTicket.id)}
                      disabled={closeTicketMutation.isPending || selectedTicket.remainingAmount > 0}
                    >
                      Close ticket
                    </Button>
                    <Button
                      variant="ghost"
                      onClick={() =>
                        reprintTicketMutation.mutate(
                          {
                            ticketId: selectedTicket.id,
                            reason: 'Operator requested receipt copy',
                            requestedBy: 'POS terminal',
                          },
                          {
                            onSuccess: (job) => setLastActionMessage(`Reprint job #${job.jobId} queued.`),
                          },
                        )
                      }
                      disabled={selectedTicket.payments.length === 0}
                    >
                      Reprint receipt
                    </Button>
                  </div>
                </div>
                <div className="grid gap-2 rounded-[1.5rem] bg-white/60 p-4">
                  <p className="text-sm text-[var(--muted-foreground)]">Settlement</p>
                  <div className="flex flex-wrap items-center gap-2">
                    <Input
                      value={paymentAmount}
                      onChange={(event) => setPaymentAmount(event.target.value)}
                      placeholder={`Default ${selectedTicket.remainingAmount.toFixed(2)}`}
                    />
                    <Button
                      onClick={() =>
                        processPaymentMutation.mutate(
                          {
                            ticketId: selectedTicket.id,
                            payload: {
                              paymentTypeId: 1,
                              amount: resolvedPaymentAmount,
                              idempotencyKey: crypto.randomUUID(),
                            },
                          },
                          {
                            onSuccess: () => setPaymentAmount(''),
                          },
                        )
                      }
                      disabled={processPaymentMutation.isPending || resolvedPaymentAmount <= 0}
                    >
                      Pay cash
                    </Button>
                    <Button
                      variant="secondary"
                      onClick={() =>
                        processPaymentMutation.mutate(
                          {
                            ticketId: selectedTicket.id,
                            payload: {
                              paymentTypeId: 2,
                              amount: resolvedPaymentAmount,
                              idempotencyKey: crypto.randomUUID(),
                            },
                          },
                          {
                            onSuccess: () => setPaymentAmount(''),
                          },
                        )
                      }
                      disabled={processPaymentMutation.isPending || resolvedPaymentAmount <= 0}
                    >
                      Pay card
                    </Button>
                  </div>
                </div>
                <div className="grid gap-2 rounded-[1.5rem] bg-white/60 p-4">
                  <p className="text-sm text-[var(--muted-foreground)]">Orders</p>
                  {selectedTicket.orders.length === 0 ? <p className="text-sm text-[var(--muted-foreground)]">No order lines yet.</p> : null}
                  {selectedTicket.orders.map((order) => (
                    <div key={order.id} className="flex flex-wrap items-center justify-between gap-2 rounded-[1rem] bg-white/70 p-3">
                      <div className="text-sm text-[var(--muted-foreground)]">
                        {order.menuItemName} x {order.quantity} ({order.status})
                      </div>
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() =>
                            updateOrderStateMutation.mutate({
                              orderId: order.id,
                              ticketId: selectedTicket.id,
                              payload: { stateName: 'Order Status', stateValue: 'Served' },
                            })
                          }
                          disabled={updateOrderStateMutation.isPending}
                        >
                          Mark served
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => voidOrderMutation.mutate({ orderId: order.id, ticketId: selectedTicket.id })}
                          disabled={voidOrderMutation.isPending}
                        >
                          Void
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
                <div className="grid gap-2 rounded-[1.5rem] bg-white/60 p-4">
                  <p className="text-sm text-[var(--muted-foreground)]">Payments</p>
                  {ticketPaymentsQuery.data?.length ? (
                    ticketPaymentsQuery.data.map((payment) => (
                      <div key={payment.id} className="flex flex-wrap items-center justify-between gap-2 rounded-[1rem] bg-white/70 p-3">
                        <div className="text-sm text-[var(--muted-foreground)]">
                          #{payment.id} {payment.paymentType} ${payment.amount.toFixed(2)}
                        </div>
                        {payment.amount > 0 ? (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() =>
                              refundPaymentMutation.mutate({
                                paymentId: payment.id,
                                ticketId: selectedTicket.id,
                                payload: {
                                  reason: 'Operator requested refund',
                                  printReceipt: true,
                                },
                              })
                            }
                            disabled={refundPaymentMutation.isPending}
                          >
                            Refund
                          </Button>
                        ) : null}
                      </div>
                    ))
                  ) : (
                    <p className="text-sm text-[var(--muted-foreground)]">No payments yet.</p>
                  )}
                </div>
                {lastActionMessage ? <div className="rounded-[1.5rem] bg-emerald-50 p-4 text-sm text-emerald-900">{lastActionMessage}</div> : null}
                {processPaymentMutation.isError ? (
                  <div className="rounded-[1.5rem] bg-amber-50 p-4 text-sm text-amber-900">{(processPaymentMutation.error as Error).message}</div>
                ) : null}
                {refundPaymentMutation.isError ? (
                  <div className="rounded-[1.5rem] bg-amber-50 p-4 text-sm text-amber-900">{(refundPaymentMutation.error as Error).message}</div>
                ) : null}
                {reprintTicketMutation.isError ? (
                  <div className="rounded-[1.5rem] bg-amber-50 p-4 text-sm text-amber-900">{(reprintTicketMutation.error as Error).message}</div>
                ) : null}
              </>
            ) : null}
            {selectedTicketQuery.isError ? (
              <div className="rounded-[1.5rem] bg-amber-50 p-4 text-sm text-amber-900">
                {(selectedTicketQuery.error as Error).message}
              </div>
            ) : null}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}