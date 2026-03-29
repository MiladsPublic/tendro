import type { TicketDto } from '@/features/terminal/api-types'
import type { TicketSummary } from '@/features/terminal/use-pos-store'

export function toTicketSummary(ticket: TicketDto, index: number): TicketSummary {
  return {
    id: ticket.id,
    label: ticket.ticketNumber || `Ticket ${ticket.id}`,
    table: `Open ticket ${index + 1}`,
    total: ticket.totalAmount,
    courses: ticket.orders.length,
    state: ticket.remainingAmount > 0 && ticket.payments.length > 0 ? 'settling' : 'open',
  }
}