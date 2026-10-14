import { vi } from 'vitest'

type Handler = (url: URL, request: Request) => Response | Promise<Response>
type Route = Handler | object

/**
 * Stubs globalThis.fetch with routes keyed by "METHOD /path".
 * A route is either a JSON body (200) or a handler. Unknown routes answer 404.
 */
export function mockApi(routes: Record<string, Route>) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(input, init)
    const url = new URL(request.url)
    const route = routes[`${request.method} ${url.pathname}`]
    if (route === undefined) return new Response(null, { status: 404 })
    return typeof route === 'function' ? (route as Handler)(url, request) : Response.json(route)
  })
}

/** Requests made to a path, as URLs. */
export function callsTo(fetchMock: ReturnType<typeof mockApi>, pathname: string, method = 'GET'): URL[] {
  return fetchMock.mock.calls
    .map(([input, init]) => (input instanceof Request ? input : new Request(input, init)))
    .filter((r) => r.method === method)
    .map((r) => new URL(r.url))
    .filter((u) => u.pathname === pathname)
}
