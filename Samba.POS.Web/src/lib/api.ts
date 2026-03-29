export type ApiError = {
  error: string
  message: string
  traceId?: string
  details?: Record<string, unknown>
}

const defaultBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() ?? ''

function buildUrl(path: string, params?: Record<string, string | number | boolean | undefined>) {
  const url = new URL(`${defaultBaseUrl}${path}`, window.location.origin)

  if (params) {
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        url.searchParams.set(key, String(value))
      }
    })
  }

  return url.toString()
}

export async function apiRequest<TResponse>(
  path: string,
  init?: RequestInit,
  params?: Record<string, string | number | boolean | undefined>,
): Promise<TResponse> {
  const response = await fetch(buildUrl(path, params), {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
  })

  if (!response.ok) {
    let payload: ApiError | undefined

    try {
      payload = (await response.json()) as ApiError
    } catch {
      payload = undefined
    }

    throw new Error(payload?.message ?? `Request failed with status ${response.status}`)
  }

  if (response.status === 204) {
    return undefined as TResponse
  }

  return (await response.json()) as TResponse
}

export const apiPaths = {
  tickets: '/api/v2/tickets',
  payments: '/api/v2/payments',
  orders: '/api/v2/orders',
  printJobs: '/api/v2/print-jobs',
} as const