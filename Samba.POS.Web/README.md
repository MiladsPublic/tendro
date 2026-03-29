# SambaPOS Web

Phase 4 monorepo web POS client for SambaPOS-3.

## Stack

- React 19
- TypeScript
- Vite 8
- React Router 7
- TanStack Query 5
- Zustand
- Tailwind CSS 4
- shadcn-compatible component structure

## Current Slices

- Overview shell
- Open ticket list via `/api/v2/tickets`
- Ticket detail via `/api/v2/tickets/{ticketId}`
- Menu ordering via `/api/v2/tickets/{ticketId}/orders`

## Run

```bash
npm install
npm run dev
```

Optional environment variable:

```bash
VITE_API_BASE_URL=http://localhost:5000
```

If `VITE_API_BASE_URL` is omitted, the app uses the current origin.

## Build

```bash
npm run build
```

## Notes

- Menu catalog data is still local because dedicated menu endpoints do not exist yet in `Samba.ApiServer.Modern`.
- Installable manifest support is enabled. Service worker integration is deferred until the Vite 8 PWA plugin ecosystem catches up.
