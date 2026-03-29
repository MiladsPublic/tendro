import { create } from 'zustand'

export type StationStatus = 'online' | 'offline' | 'syncing'

export type TicketSummary = {
  id: number
  label: string
  table: string
  total: number
  courses: number
  state: 'open' | 'settling'
}

type PosState = {
  stationName: string
  departmentId: number
  terminalId: number
  operatorName: string
  status: StationStatus
  pendingQueueCount: number
  lastSyncLabel: string
  selectedCategory: string
  selectedTicketId: number | null
  categories: string[]
  setSelectedCategory: (category: string) => void
  setSelectedTicketId: (ticketId: number | null) => void
  setOperatorName: (operatorName: string) => void
  cycleConnectionState: () => void
  drainQueue: () => void
}

const connectionCycle: Record<StationStatus, StationStatus> = {
  online: 'syncing',
  syncing: 'offline',
  offline: 'online',
}

export const usePosStore = create<PosState>((set) => ({
  stationName: 'Front Terrace T2',
  departmentId: 1,
  terminalId: 1,
  operatorName: 'Ayse',
  status: 'online',
  pendingQueueCount: 3,
  lastSyncLabel: 'Synced 12 seconds ago',
  selectedCategory: 'Grill',
  selectedTicketId: null,
  categories: ['Grill', 'Salads', 'Coffee', 'Desserts'],
  setSelectedCategory: (selectedCategory) => set({ selectedCategory }),
  setSelectedTicketId: (selectedTicketId) => set({ selectedTicketId }),
  setOperatorName: (operatorName) => set({ operatorName }),
  cycleConnectionState: () =>
    set((state) => {
      const nextStatus = connectionCycle[state.status]

      return {
        status: nextStatus,
        lastSyncLabel: nextStatus === 'online' ? 'Recovered just now' : state.lastSyncLabel,
      }
    }),
  drainQueue: () => set({ pendingQueueCount: 0, lastSyncLabel: 'Queue drained locally' }),
}))