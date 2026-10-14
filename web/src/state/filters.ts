import type { ItemsQuery, WorkItemStatus } from '../api/client'

export const STATUSES: readonly WorkItemStatus[] = ['Todo', 'InProgress', 'Blocked', 'Done']

export const SORT_OPTIONS = [
  { value: 'priority', label: 'Priorité' },
  { value: 'dueDate', label: 'Échéance' },
  { value: '-updatedAt', label: 'Mise à jour récente' },
  { value: 'status', label: 'Statut' },
  { value: 'title', label: 'Titre' },
] as const
export type SortKey = (typeof SORT_OPTIONS)[number]['value']

export const GROUP_OPTIONS = [
  { value: 'none', label: 'Aucun' },
  { value: 'source', label: 'Source' },
  { value: 'project', label: 'Projet' },
] as const
export type GroupBy = (typeof GROUP_OPTIONS)[number]['value']

export type Filters = {
  sources: string[]
  projects: string[]
  statuses: WorkItemStatus[]
  q: string
  sort: SortKey
  group: GroupBy
}

export const DEFAULT_FILTERS: Filters = {
  sources: [],
  projects: [],
  statuses: [],
  q: '',
  // Native priority until the unified score exists (phase 6).
  sort: 'priority',
  group: 'none',
}

/** The whole list is loaded at once: a personal task list stays well under this. */
export const PAGE_SIZE = 200

const isStatus = (value: string): value is WorkItemStatus => (STATUSES as readonly string[]).includes(value)
const isSort = (value: string | null): value is SortKey => SORT_OPTIONS.some((o) => o.value === value)
const isGroup = (value: string | null): value is GroupBy => GROUP_OPTIONS.some((o) => o.value === value)

const unique = (values: string[]) => [...new Set(values.map((v) => v.trim()).filter(Boolean))]

/** Reads filters from a query string, ignoring unknown or invalid values. */
export function parseFilters(search: string): Filters {
  const params = new URLSearchParams(search)
  const sort = params.get('sort')
  const group = params.get('group')
  return {
    sources: unique(params.getAll('source')),
    projects: unique(params.getAll('project')),
    statuses: unique(params.getAll('status')).filter(isStatus),
    q: params.get('q')?.trim() ?? '',
    sort: isSort(sort) ? sort : DEFAULT_FILTERS.sort,
    group: isGroup(group) ? group : DEFAULT_FILTERS.group,
  }
}

/** Writes filters as a query string ("" when everything is default), omitting default values. */
export function serializeFilters(filters: Filters): string {
  const params = new URLSearchParams()
  filters.sources.forEach((s) => params.append('source', s))
  filters.projects.forEach((p) => params.append('project', p))
  filters.statuses.forEach((s) => params.append('status', s))
  if (filters.q.trim()) params.set('q', filters.q.trim())
  if (filters.sort !== DEFAULT_FILTERS.sort) params.set('sort', filters.sort)
  if (filters.group !== DEFAULT_FILTERS.group) params.set('group', filters.group)
  const query = params.toString()
  return query ? `?${query}` : ''
}

/** Maps UI filters to GET /api/items parameters. Grouping is client-side only. */
export function toItemsQuery(filters: Filters): ItemsQuery {
  return {
    source: filters.sources.length > 0 ? filters.sources : undefined,
    project: filters.projects.length > 0 ? filters.projects : undefined,
    status: filters.statuses.length > 0 ? filters.statuses : undefined,
    q: filters.q.trim() || undefined,
    sort: filters.sort,
    pageSize: PAGE_SIZE,
  }
}

export function hasActiveFilters(filters: Filters): boolean {
  return filters.sources.length + filters.projects.length + filters.statuses.length > 0 || filters.q.trim() !== ''
}

export function toggleValue<T>(values: readonly T[], value: T): T[] {
  return values.includes(value) ? values.filter((v) => v !== value) : [...values, value]
}
