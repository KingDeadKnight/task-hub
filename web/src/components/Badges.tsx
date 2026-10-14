import type { ScoreReason, WorkItemStatus } from '../api/client'
import { formatReason, scoreLevel, sourceLabel, STATUS_LABELS, type ScoreLevel } from '../lib/format'

const SCORE_STYLES: Record<ScoreLevel, string> = {
  high: 'bg-red-600 text-white dark:bg-red-500',
  medium: 'bg-amber-400 text-amber-950 dark:bg-amber-500',
  low: 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200',
}

/** Unified priority score (0–100); the reasons are in the tooltip and for screen readers. */
export function ScoreBadge({ score, reasons }: { score: number; reasons: ScoreReason[] }) {
  const explanation = reasons.length > 0 ? reasons.map(formatReason).join(', ') : 'Aucun facteur particulier'
  return (
    <span
      title={`Score de priorité ${score}/100 : ${explanation}`}
      className={`inline-flex min-w-9 shrink-0 justify-center rounded-md px-1.5 py-0.5 text-xs font-semibold tabular-nums ${SCORE_STYLES[scoreLevel(score)]}`}
    >
      {score}
      <span className="sr-only"> sur 100 : {explanation}</span>
    </span>
  )
}

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
