import type { WorkItem } from '../api/client'
import type { GroupBy } from '../state/filters'

export type ItemGroup = {
  key: string
  label: string
  items: WorkItem[]
}

/**
 * Groups items by source or project. Groups are sorted by label; items keep the order
 * they came in (the server sort), so sorting still applies inside each group.
 */
export function groupItems(items: readonly WorkItem[], by: GroupBy, labelOf: (key: string) => string = (k) => k): ItemGroup[] {
  if (by === 'none') return [{ key: '', label: '', items: [...items] }]

  const groups = new Map<string, WorkItem[]>()
  for (const item of items) {
    const key = by === 'source' ? item.source : item.project
    const group = groups.get(key)
    if (group) group.push(item)
    else groups.set(key, [item])
  }

  return [...groups.entries()]
    .map(([key, groupItems]) => ({ key, label: labelOf(key) || 'Sans projet', items: groupItems }))
    .sort((a, b) => a.label.localeCompare(b.label, 'fr', { sensitivity: 'base' }))
}
