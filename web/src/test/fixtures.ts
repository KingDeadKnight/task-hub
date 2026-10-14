import type { SourcesStatus, WorkItem, WorkItemFacets, WorkItemPage } from '../api/client'

export function item(overrides: Partial<WorkItem> & Pick<WorkItem, 'id'>): WorkItem {
  const [source, externalId] = overrides.id.split(':')
  return {
    source,
    externalId,
    title: `Item ${externalId}`,
    url: `https://example.test/${externalId}`,
    project: 'Web',
    status: 'Todo',
    rawStatus: 'New',
    nativePriority: 2,
    dueDate: null,
    updatedAt: '2026-10-01T09:00:00Z',
    lastSyncedAt: '2026-10-14T08:00:00Z',
    ...overrides,
  }
}

export const ITEMS: WorkItem[] = [
  item({ id: 'azuredevops:1', title: 'Fix login bug', status: 'InProgress', rawStatus: 'Active', nativePriority: 1, dueDate: '2020-01-15' }),
  item({ id: 'azuredevops:2', title: 'Write docs', project: 'Docs', dueDate: '2099-12-31' }),
  item({ id: 'jira:A-1', title: 'Review contract', project: 'Mobile', status: 'Blocked', rawStatus: 'On hold', nativePriority: null }),
]

export function page(items: WorkItem[] = ITEMS, totalCount = items.length): WorkItemPage {
  return { items, page: 1, pageSize: 200, totalCount, totalPages: 1 }
}

export const FACETS: WorkItemFacets = {
  sources: [
    { value: 'azuredevops', count: 2 },
    { value: 'jira', count: 1 },
  ],
  projects: [
    { value: 'Docs', count: 1 },
    { value: 'Mobile', count: 1 },
    { value: 'Web', count: 1 },
  ],
  statuses: [
    { value: 'Todo', count: 1 },
    { value: 'InProgress', count: 1 },
    { value: 'Blocked', count: 1 },
  ],
}

export const SOURCES: SourcesStatus = {
  isSyncing: false,
  sources: [
    {
      name: 'azuredevops',
      activeItemCount: 2,
      lastSuccessAt: '2026-10-14T08:00:00Z',
      lastRun: { startedAt: '2026-10-14T07:59:58Z', finishedAt: '2026-10-14T08:00:00Z', succeeded: true, itemCount: 2, error: null },
    },
    {
      name: 'jira',
      activeItemCount: 1,
      lastSuccessAt: '2026-10-13T08:00:00Z',
      lastRun: { startedAt: '2026-10-14T07:59:58Z', finishedAt: '2026-10-14T08:00:00Z', succeeded: false, itemCount: 0, error: 'HTTP 401' },
    },
  ],
}
