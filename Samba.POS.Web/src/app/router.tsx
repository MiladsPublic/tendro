import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/app/shell'
import { DashboardPage } from '@/features/terminal/dashboard-page'
import { MenuPage } from '@/features/terminal/menu-page'
import { TicketsPage } from '@/features/terminal/tickets-page'

export const appRouter = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      {
        index: true,
        element: <DashboardPage />,
      },
      {
        path: 'tickets',
        element: <TicketsPage />,
      },
      {
        path: 'menu',
        element: <MenuPage />,
      },
    ],
  },
])