import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

// Types generated from web/openapi/taskhub.json (npm run gen:api).
type Schemas = components['schemas']
export type WorkItem = Schemas['WorkItemDto']
export type WorkItemPage = Schemas['WorkItemPageDto']
export type WorkItemStatus = Schemas['WorkItemStatus']
export type WorkItemFacets = Schemas['WorkItemFacetsDto']
export type SourcesStatus = Schemas['SourcesStatusDto']
export type SourceStatus = Schemas['SourceStatusDto']
export type SyncResponse = Schemas['SyncResponseDto']
export type ScoreReason = Schemas['ScoreReasonDto']
export type Today = Schemas['TodayDto']
export type ItemsQuery =NonNullable<paths['/api/items']['get']['parameters']['query']>

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

const client = createClient<paths>({
  baseUrl: globalThis.location?.origin ?? '',
  // Looked up at call time (not captured at creation) so tests can stub globalThis.fetch.
  fetch: (request) => globalThis.fetch(request),
})

type ClientResult<T> = { data?: T; error?: unknown; response: Response }

function ensure<T>({ data, error, response }: ClientResult<T>): T {
  if (data !== undefined) return data
  const problem = error as { detail?: string | null; title?: string | null } | undefined
  throw new ApiError(problem?.detail ?? problem?.title ?? `Erreur HTTP ${response.status}`, response.status)
}

export const api = {
  items: async (query: ItemsQuery, signal?: AbortSignal): Promise<WorkItemPage> =>
    ensure(await client.GET('/api/items', { params: { query }, signal })),

  today: async (signal?: AbortSignal): Promise<Today> =>
    ensure(await client.GET('/api/items/today', { signal })),

  facets: async (signal?: AbortSignal): Promise<WorkItemFacets> =>
    ensure(await client.GET('/api/items/facets', { signal })),

  sources: async (signal?: AbortSignal): Promise<SourcesStatus> =>
    ensure(await client.GET('/api/sources', { signal })),

  sync: async (): Promise<SyncResponse> => ensure(await client.POST('/api/sync')),
}
