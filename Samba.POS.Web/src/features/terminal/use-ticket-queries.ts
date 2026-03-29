import { startTransition } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiPaths, apiRequest } from '@/lib/api'
import type {
  AddOrderRequest,
  CreateTicketRequest,
  PagedResponse,
  PaymentDto,
  PrintJobDto,
  ProcessPaymentRequest,
  ReprintTicketRequest,
  RefundPaymentRequest,
  TicketDto,
  UpdateOrderStateRequest,
  UpdateTicketStateRequest,
} from '@/features/terminal/api-types'
import { usePosStore } from '@/features/terminal/use-pos-store'

const ticketsQueryKey = (departmentId: number) => ['tickets', departmentId] as const
const ticketDetailQueryKey = (ticketId: number | null) => ['ticket', ticketId] as const
const ticketPaymentsQueryKey = (ticketId: number | null) => ['ticket-payments', ticketId] as const

export function useOpenTickets() {
  const departmentId = usePosStore((state) => state.departmentId)

  return useQuery({
    queryKey: ticketsQueryKey(departmentId),
    queryFn: () =>
      apiRequest<PagedResponse<TicketDto>>(apiPaths.tickets, undefined, {
        departmentId,
        pageNumber: 1,
        pageSize: 20,
      }),
  })
}

export function useSelectedTicket(ticketId: number | null) {
  return useQuery({
    queryKey: ticketDetailQueryKey(ticketId),
    enabled: ticketId !== null,
    queryFn: () => apiRequest<TicketDto>(`${apiPaths.tickets}/${ticketId}`),
  })
}

export function useCreateTicket() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)
  const terminalId = usePosStore((state) => state.terminalId)
  const setSelectedTicketId = usePosStore((state) => state.setSelectedTicketId)

  return useMutation({
    mutationFn: () =>
      apiRequest<TicketDto>(apiPaths.tickets, {
        method: 'POST',
        body: JSON.stringify({
          departmentId,
          terminalId,
          ticketTypeId: null,
        } satisfies CreateTicketRequest),
      }),
    onSuccess: async (ticket) => {
      startTransition(() => {
        setSelectedTicketId(ticket.id)
      })

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.setQueryData(ticketDetailQueryKey(ticket.id), ticket),
      ])
    },
  })
}

export function useTicketPayments(ticketId: number | null) {
  return useQuery({
    queryKey: ticketPaymentsQueryKey(ticketId),
    enabled: ticketId !== null,
    queryFn: () => apiRequest<PaymentDto[]>(`${apiPaths.payments}/ticket/${ticketId}`),
  })
}

export function useAddOrder() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)
  const setSelectedTicketId = usePosStore((state) => state.setSelectedTicketId)

  return useMutation({
    mutationFn: ({ ticketId, payload }: { ticketId: number; payload: AddOrderRequest }) =>
      apiRequest<TicketDto>(`${apiPaths.tickets}/${ticketId}/orders`, {
        method: 'POST',
        body: JSON.stringify(payload),
      }),
    onSuccess: async (ticket) => {
      startTransition(() => {
        setSelectedTicketId(ticket.id)
      })

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.setQueryData(ticketDetailQueryKey(ticket.id), ticket),
      ])
    },
  })
}

export function useUpdateTicketState() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)

  return useMutation({
    mutationFn: ({ ticketId, payload }: { ticketId: number; payload: UpdateTicketStateRequest }) =>
      apiRequest<TicketDto>(`${apiPaths.tickets}/${ticketId}/state`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      }),
    onSuccess: async (ticket) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.setQueryData(ticketDetailQueryKey(ticket.id), ticket),
      ])
    },
  })
}

export function useCloseTicket() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)
  const setSelectedTicketId = usePosStore((state) => state.setSelectedTicketId)

  return useMutation({
    mutationFn: (ticketId: number) =>
      apiRequest<TicketDto>(`${apiPaths.tickets}/${ticketId}/close`, {
        method: 'POST',
      }),
    onSuccess: async (ticket) => {
      startTransition(() => {
        setSelectedTicketId(null)
      })

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.setQueryData(ticketDetailQueryKey(ticket.id), ticket),
      ])
    },
  })
}

export function useProcessPayment() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)

  return useMutation({
    mutationFn: ({ ticketId, payload }: { ticketId: number; payload: ProcessPaymentRequest }) =>
      apiRequest<PaymentDto>(apiPaths.payments, {
        method: 'POST',
        body: JSON.stringify(payload),
      }, {
        ticketId,
      }),
    onSuccess: async (_, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.invalidateQueries({ queryKey: ticketDetailQueryKey(variables.ticketId) }),
        queryClient.invalidateQueries({ queryKey: ticketPaymentsQueryKey(variables.ticketId) }),
      ])
    },
  })
}

export function useRefundPayment() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)

  return useMutation({
    mutationFn: ({ paymentId, ticketId, payload }: { paymentId: number; ticketId: number; payload: RefundPaymentRequest }) =>
      apiRequest<PaymentDto>(`${apiPaths.payments}/${paymentId}/refund`, {
        method: 'POST',
        body: JSON.stringify(payload),
      }).then((result) => ({ result, ticketId })),
    onSuccess: async ({ ticketId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.invalidateQueries({ queryKey: ticketDetailQueryKey(ticketId) }),
        queryClient.invalidateQueries({ queryKey: ticketPaymentsQueryKey(ticketId) }),
      ])
    },
  })
}

export function useUpdateOrderState() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)

  return useMutation({
    mutationFn: ({ orderId, ticketId, payload }: { orderId: number; ticketId: number; payload: UpdateOrderStateRequest }) =>
      apiRequest(`${apiPaths.orders}/${orderId}/state`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      }).then((result) => ({ result, ticketId })),
    onSuccess: async ({ ticketId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.invalidateQueries({ queryKey: ticketDetailQueryKey(ticketId) }),
      ])
    },
  })
}

export function useVoidOrder() {
  const queryClient = useQueryClient()
  const departmentId = usePosStore((state) => state.departmentId)

  return useMutation({
    mutationFn: ({ orderId, ticketId }: { orderId: number; ticketId: number }) =>
      apiRequest(`${apiPaths.orders}/${orderId}/void`, {
        method: 'POST',
      }).then((result) => ({ result, ticketId })),
    onSuccess: async ({ ticketId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey(departmentId) }),
        queryClient.invalidateQueries({ queryKey: ticketDetailQueryKey(ticketId) }),
      ])
    },
  })
}

export function useReprintTicket() {
  return useMutation({
    mutationFn: (payload: ReprintTicketRequest) =>
      apiRequest<PrintJobDto>(`${apiPaths.printJobs}/reprint`, {
        method: 'POST',
        body: JSON.stringify(payload),
      }),
  })
}