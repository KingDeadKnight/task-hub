import type { ReactNode } from 'react'
import type { WorkItem } from '../api/client'
import { useItems } from '../api/queries'
import { formatDueDate, isOverdue, sourceLabel } from '../lib/format'
import { groupItems } from '../lib/grouping'
import { hasActiveFilters, type Filters } from '../state/filters'
import { ScoreBadge, SourceBadge, StatusBadge } from './Badges'
import { ExternalIcon } from './icons'

const panel = 'rounded-lg border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900'

function DueDate({ dueDate }: { dueDate: string }) {
  const overdue = isOverdue(dueDate)
  return (
    <span
      data-overdue={overdue || undefined}
      className={`text-xs whitespace-nowrap ${overdue ? 'font-semibold text-red-600 dark:text-red-400' : 'text-slate-500 dark:text-slate-400'}`}
    >
      {overdue ? 'En retard · ' : 'Échéance '}
      <time dateTime={dueDate}>{formatDueDate(dueDate)}</time>
    </span>
  )
}

function ItemRow({ item }: { item: WorkItem }) {
  return (
    // Phone: badges on top, title full width. From sm: source | title + meta | status + due date.
    <li className="grid grid-cols-[1fr_auto] items-start gap-x-3 gap-y-1.5 px-4 py-3 hover:bg-slate-50 sm:grid-cols-[auto_1fr_auto] dark:hover:bg-slate-800/50">
      <div className="col-start-1 row-start-1 flex items-center gap-2 justify-self-start">
        <ScoreBadge score={item.score} reasons={item.scoreReasons} />
        <SourceBadge source={item.source} />
      </div>
      <div className="col-span-2 row-start-2 min-w-0 sm:col-span-1 sm:col-start-2 sm:row-start-1">
        <a
          href={item.url}
          target="_blank"
          rel="noopener noreferrer"
          className="group font-medium [overflow-wrap:anywhere] hover:text-indigo-600 hover:underline dark:hover:text-indigo-400"
        >
          {item.title}
          <ExternalIcon className="ml-1 inline size-3.5 align-[-2px] opacity-40 group-hover:opacity-100" />
          <span className="sr-only">(ouvre {sourceLabel(item.source)} dans un nouvel onglet)</span>
        </a>
        <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500 dark:text-slate-400">
          <span>{item.project || 'Sans projet'}</span>
          <span>#{item.externalId}</span>
          {item.nativePriority !== null && <span title="Priorité native">P{item.nativePriority}</span>}
          <span title="Statut dans l’outil d’origine">{item.rawStatus}</span>
        </div>
      </div>
      <div className="col-start-2 row-start-1 flex items-center gap-2 justify-self-end sm:col-start-3 sm:flex-col sm:items-end sm:gap-1">
        <StatusBadge status={item.status} />
        {item.dueDate !== null && <DueDate dueDate={item.dueDate} />}
      </div>
    </li>
  )
}

function Skeleton() {
  return (
    <div className={panel} aria-busy="true">
      <span className="sr-only">Chargement des tâches…</span>
      <ul className="divide-y divide-slate-100 dark:divide-slate-800">
        {Array.from({ length: 5 }, (_, i) => (
          <li key={i} className="flex animate-pulse items-center gap-3 px-4 py-4">
            <span className="h-5 w-20 rounded bg-slate-200 dark:bg-slate-800" />
            <span className="h-4 flex-1 rounded bg-slate-200 dark:bg-slate-800" />
            <span className="h-5 w-16 rounded-full bg-slate-200 dark:bg-slate-800" />
          </li>
        ))}
      </ul>
    </div>
  )
}

function Message({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className={`${panel} px-6 py-12 text-center`}>
      <p className="font-medium">{title}</p>
      {children && <div className="mt-2 text-sm text-slate-500 dark:text-slate-400">{children}</div>}
    </div>
  )
}

export function ItemList({ filters, onReset }: { filters: Filters; onReset: () => void }) {
  const { data, isPending, isError, error, isFetching, refetch } = useItems(filters)

  if (isPending) return <Skeleton />

  if (isError && !data) {
    return (
      <div role="alert">
        <Message title="Impossible de charger les tâches.">
          <p>{error.message}</p>
          <button
            type="button"
            onClick={() => void refetch()}
            className="mt-4 rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            Réessayer
          </button>
        </Message>
      </div>
    )
  }

  if (data.items.length === 0) {
    return hasActiveFilters(filters) ? (
      <Message title="Aucune tâche ne correspond à ces filtres.">
        <button type="button" onClick={onReset} className="text-indigo-600 hover:underline dark:text-indigo-400">
          Réinitialiser les filtres
        </button>
      </Message>
    ) : (
      <Message title="Aucune tâche ne vous est assignée.">
        <p>Les sources sont synchronisées automatiquement ; utilisez « Synchroniser » pour forcer une mise à jour.</p>
      </Message>
    )
  }

  const groups = groupItems(data.items, filters.group, filters.group === 'source' ? sourceLabel : undefined)

  return (
    <section aria-label="Tâches" aria-busy={isFetching} className="space-y-4">
      <div className="flex items-center justify-between text-sm text-slate-500 dark:text-slate-400">
        <p>
          {data.totalCount} tâche{data.totalCount > 1 ? 's' : ''}
          {data.totalCount > data.items.length && ` (affichage des ${data.items.length} premières)`}
        </p>
        {isError && <p className="text-amber-700 dark:text-amber-400">Mise à jour impossible, données peut-être obsolètes.</p>}
      </div>

      {groups.map((group) => (
        <div key={group.key} className={panel}>
          {filters.group !== 'none' && (
            <h2 className="flex items-center justify-between border-b border-slate-200 px-4 py-2 text-sm font-semibold dark:border-slate-800">
              {group.label}
              <span className="font-normal text-slate-500">{group.items.length}</span>
            </h2>
          )}
          <ul className="divide-y divide-slate-100 dark:divide-slate-800">
            {group.items.map((item) => (
              <ItemRow key={item.id} item={item} />
            ))}
          </ul>
        </div>
      ))}
    </section>
  )
}
