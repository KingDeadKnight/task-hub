import { useEffect, useEffectEvent, useId, useRef, useState, type ReactNode } from 'react'
import type { WorkItemStatus } from '../api/client'
import { useFacets } from '../api/queries'
import { sourceLabel, STATUS_LABELS } from '../lib/format'
import {
  GROUP_OPTIONS,
  hasActiveFilters,
  SORT_OPTIONS,
  STATUSES,
  toggleValue,
  type Filters,
  type GroupBy,
  type SortKey,
} from '../state/filters'
import { ChevronDownIcon, SearchIcon } from './icons'

export const SEARCH_DEBOUNCE_MS = 300

type UpdateFilters = (patch: Partial<Filters>, options?: { replace?: boolean }) => void

type Props = {
  filters: Filters
  onChange: UpdateFilters
  onReset: () => void
}

function Chip({ pressed, onClick, children }: { pressed: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onClick}
      className={`rounded-full px-3 py-1 text-sm ring-1 ring-inset transition-colors focus-visible:outline-2 focus-visible:outline-indigo-500 ${
        pressed
          ? 'bg-indigo-600 text-white ring-indigo-600 dark:bg-indigo-500 dark:ring-indigo-500'
          : 'bg-white text-slate-700 ring-slate-300 hover:bg-slate-50 dark:bg-slate-900 dark:text-slate-300 dark:ring-slate-700 dark:hover:bg-slate-800'
      }`}
    >
      {children}
    </button>
  )
}

function Count({ value }: { value: number | undefined }) {
  return value === undefined ? null : <span className="ml-1 opacity-70">{value}</span>
}

function SearchBox({ value, onSearch }: { value: string; onSearch: (q: string) => void }) {
  const [text, setText] = useState(value)
  const [syncedValue, setSyncedValue] = useState(value)

  // Follow external changes (reset, back/forward) without clobbering what is being typed.
  if (value !== syncedValue) {
    setSyncedValue(value)
    if (value !== text.trim()) setText(value)
  }

  const commit = useEffectEvent((q: string) => onSearch(q))

  useEffect(() => {
    if (text.trim() === value) return
    const timer = setTimeout(() => commit(text.trim()), SEARCH_DEBOUNCE_MS)
    return () => clearTimeout(timer)
  }, [text, value])

  return (
    <div className="relative flex-1">
      <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" />
      <input
        type="search"
        aria-label="Rechercher"
        placeholder="Rechercher par titre, numéro ou projet…"
        value={text}
        onChange={(e) => setText(e.target.value)}
        className="w-full rounded-md border border-slate-300 bg-white py-2 pr-3 pl-9 text-sm placeholder:text-slate-400 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-slate-700 dark:bg-slate-900"
      />
    </div>
  )
}

function ProjectPicker({
  projects,
  selected,
  onChange,
}: {
  projects: { value: string; count: number }[]
  selected: string[]
  onChange: (projects: string[]) => void
}) {
  const ref = useRef<HTMLDetailsElement>(null)

  useEffect(() => {
    const close = (event: MouseEvent) => {
      if (ref.current?.open && !ref.current.contains(event.target as Node)) ref.current.open = false
    }
    document.addEventListener('click', close)
    return () => document.removeEventListener('click', close)
  }, [])

  // A project selected in the URL may no longer have active items: keep it visible so it can be unchecked.
  const options = [
    ...projects,
    ...selected.filter((p) => !projects.some((o) => o.value === p)).map((value) => ({ value, count: 0 })),
  ]

  return (
    <details ref={ref} className="relative">
      <summary className="flex cursor-pointer list-none items-center gap-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-900 dark:hover:bg-slate-800 [&::-webkit-details-marker]:hidden">
        Projets{selected.length > 0 && <span className="font-medium"> ({selected.length})</span>}
        <ChevronDownIcon />
      </summary>
      <div className="absolute z-10 mt-1 max-h-72 w-72 overflow-auto rounded-md border border-slate-200 bg-white p-1 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        {options.length === 0 && <p className="px-2 py-1.5 text-sm text-slate-500">Aucun projet</p>}
        {options.map((p) => (
          <label
            key={p.value}
            className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            <input
              type="checkbox"
              checked={selected.includes(p.value)}
              onChange={() => onChange(toggleValue(selected, p.value))}
              className="accent-indigo-600"
            />
            <span className="min-w-0 flex-1 truncate">{p.value || 'Sans projet'}</span>
            <span className="text-xs text-slate-500">{p.count}</span>
          </label>
        ))}
      </div>
    </details>
  )
}

function Select<T extends string>({
  label,
  value,
  options,
  onChange,
}: {
  label: string
  value: T
  options: readonly { value: T; label: string }[]
  onChange: (value: T) => void
}) {
  const id = useId()
  return (
    <div className="flex items-center gap-2 text-sm">
      <label htmlFor={id} className="text-slate-500 dark:text-slate-400">
        {label}
      </label>
      <select
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value as T)}
        className="rounded-md border border-slate-300 bg-white py-1.5 pr-8 pl-2 text-sm dark:border-slate-700 dark:bg-slate-900"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </div>
  )
}

export function FilterBar({ filters, onChange, onReset }: Props) {
  const { data: facets } = useFacets()
  const statusCount = (status: WorkItemStatus) => facets?.statuses.find((s) => s.value === status)?.count ?? 0
  const sources = facets?.sources ?? []

  return (
    <section aria-label="Filtres" className="space-y-3">
      <SearchBox value={filters.q} onSearch={(q) => onChange({ q }, { replace: true })} />

      <div className="flex flex-wrap items-center gap-2">
        {sources.length > 1 && (
          <div role="group" aria-label="Sources" className="flex flex-wrap gap-2">
            {sources.map((s) => (
              <Chip
                key={s.value}
                pressed={filters.sources.includes(s.value)}
                onClick={() => onChange({ sources: toggleValue(filters.sources, s.value) })}
              >
                {sourceLabel(s.value)}
                <Count value={s.count} />
              </Chip>
            ))}
          </div>
        )}

        <div role="group" aria-label="Statuts" className="flex flex-wrap gap-2">
          {STATUSES.map((status) => (
            <Chip
              key={status}
              pressed={filters.statuses.includes(status)}
              onClick={() => onChange({ statuses: toggleValue(filters.statuses, status) })}
            >
              {STATUS_LABELS[status]}
              <Count value={facets ? statusCount(status) : undefined} />
            </Chip>
          ))}
        </div>

        <ProjectPicker
          projects={facets?.projects ?? []}
          selected={filters.projects}
          onChange={(projects) => onChange({ projects })}
        />

        {hasActiveFilters(filters) && (
          <button
            type="button"
            onClick={onReset}
            className="text-sm text-indigo-600 hover:underline dark:text-indigo-400"
          >
            Réinitialiser les filtres
          </button>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
        <Select<SortKey> label="Trier par" value={filters.sort} options={SORT_OPTIONS} onChange={(sort) => onChange({ sort })} />
        <Select<GroupBy> label="Grouper par" value={filters.group} options={GROUP_OPTIONS} onChange={(group) => onChange({ group })} />
      </div>
    </section>
  )
}
