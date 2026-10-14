import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toItemsQuery, type Filters } from '../state/filters'
import { api } from './client'

export const REFRESH_INTERVAL_MS = 60_000
const SYNCING_POLL_MS = 3_000

export const queryKeys = {
  items: (filters: Filters) => ['items', toItemsQuery(filters)] as const,
  facets: ['facets'] as const,
  sources: ['sources'] as const,
}

export function useItems(filters: Filters) {
  return useQuery({
    queryKey: queryKeys.items(filters),
    queryFn: ({ signal }) => api.items(toItemsQuery(filters), signal),
    refetchInterval: REFRESH_INTERVAL_MS,
    // Keep showing the current list while a new filter combination loads.
    placeholderData: keepPreviousData,
  })
}

export function useFacets() {
  return useQuery({
    queryKey: queryKeys.facets,
    queryFn: ({ signal }) => api.facets(signal),
    refetchInterval: REFRESH_INTERVAL_MS,
  })
}

export function useSources() {
  return useQuery({
    queryKey: queryKeys.sources,
    queryFn: ({ signal }) => api.sources(signal),
    // Poll faster while a sync (background or manual) is running.
    refetchInterval: (query) => (query.state.data?.isSyncing ? SYNCING_POLL_MS : REFRESH_INTERVAL_MS),
  })
}

export function useSync() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: api.sync,
    onSettled: () => queryClient.invalidateQueries(),
  })
}
