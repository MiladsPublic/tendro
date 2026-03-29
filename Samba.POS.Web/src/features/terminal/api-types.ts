export type OrderDto = {
  id: number
  menuItemId: number
  menuItemName: string
  quantity: number
  unitPrice: number
  lineTotal: number
  status: string
}

export type PaymentDto = {
  id: number
  amount: number
  processedAt: string
  paymentType: string
}

export type TicketDto = {
  id: number
  ticketNumber: string
  issuedAt: string
  totalAmount: number
  remainingAmount: number
  isClosed: boolean
  orders: OrderDto[]
  payments: PaymentDto[]
}

export type PagedResponse<TItem> = {
  items: TItem[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type CreateTicketRequest = {
  departmentId: number
  terminalId: number
  ticketTypeId?: number | null
}

export type AddOrderRequest = {
  menuItemId: number
  quantity: number
  portionName?: string | null
  tags?: Record<string, string> | null
}

export type UpdateTicketStateRequest = {
  stateName: string
  stateValue: string
}

export type UpdateOrderStateRequest = {
  stateName: string
  stateValue: string
}

export type ProcessPaymentRequest = {
  paymentTypeId: number
  amount: number
  tenderedAmount?: number | null
  idempotencyKey: string
}

export type RefundPaymentRequest = {
  reason: string
  printReceipt?: boolean
}

export type ReprintTicketRequest = {
  ticketId: number
  reason?: string | null
  requestedBy?: string | null
}

export type PrintJobDto = {
  jobId: number
  ticketId: number
  jobType: string
  status: string
  createdAtUtc: string
  reason?: string | null
  requestedBy?: string | null
}