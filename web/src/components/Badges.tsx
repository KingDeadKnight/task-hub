import type { WorkItemStatus } from '../api/client'
import { sourceLabel, STATUS_LABELS } from '../lib/format'

const SOURCE_STYLES: Record<string, string> = {
  azuredevops: 'bg-sky-100 text-sky-800 ring-sky-200 dark:bg-sky-950 dark:text-sky-300 dark:ring-sky-900',
  jira: 'bg-indigo-100 text-indigo-800 ring-indigo-200 dark:bg-indigo-950 dark:text-indigo-300 dark:ring-indigo-900',
}
const DEFAULT_SOURCE_STYLE = 'bg-slate-100 text-slate-700 ring-slate-200 dark:bg-slate-800 dark:text-slate-300 dark:ring-slate-700'

export function SourceBadge({ source }: { source: string }) {
  return (
    <span
      className={`inline-flex shrink-0 items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${SOURCE_STYLES[source] ?? DEFAULT_SOURCE_STYLE}`}
    >
      {sourceLabel(source)}
    </span>
  )
}

const STATUS_STYLES: Record<WorkItemStatus, string> = {
  Todo: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
  InProgress: 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300',
  Blocked: 'bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300',
  Done: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
}

export function StatusBadge({ status }: { status: WorkItemStatus }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[status]}`}>
      {STATUS_LABELS[status]}
    </span>
  )
}
