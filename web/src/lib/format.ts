import type { ScoreReason, WorkItemStatus } from '../api/client'

const SOURCE_LABELS: Record<string, string> = {
  azuredevops: 'Azure DevOps',
  jira: 'Jira',
}

export function sourceLabel(source: string): string {
  return SOURCE_LABELS[source] ?? source
}

export const STATUS_LABELS: Record<WorkItemStatus, string> = {
  Todo: 'À faire',
  InProgress: 'En cours',
  Blocked: 'Bloqué',
  Done: 'Terminé',
}

const days = (n: number | null) => `${n ?? 0} j`

/** Words a score reason sent by the API. */
export function formatReason(reason: ScoreReason): string {
  switch (reason.kind) {
    case 'Overdue':
      return `En retard de ${days(reason.days)}`
    case 'DueToday':
      return 'Échéance aujourd’hui'
    case 'DueSoon':
      return reason.days === 1 ? 'Échéance demain' : `Échéance dans ${days(reason.days)}`
    case 'HighPriority':
      return `Priorité ${reason.priority ?? 1}`
    case 'InProgress':
      return 'En cours'
    case 'Blocked':
      return 'Bloqué'
    case 'Stale':
      return `Sans mise à jour depuis ${days(reason.days)}`
  }
}

export type ScoreLevel = 'high' | 'medium' | 'low'

export function scoreLevel(score: number): ScoreLevel {
  return score >= 70 ? 'high' : score >= 40 ? 'medium' : 'low'
}

/** Today as "YYYY-MM-DD" in local time, comparable with API due dates. */
export function todayIso(now: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

/** A due date strictly before today is overdue (due today is not). */
export function isOverdue(dueDate: string | null, today: string = todayIso()): boolean {
  return dueDate !== null && dueDate < today
}

const dueDateFormat = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short', year: 'numeric' })

/** Formats a "YYYY-MM-DD" date without shifting it through UTC. */
export function formatDueDate(dueDate: string): string {
  const [year, month, day] = dueDate.split('-').map(Number)
  return dueDateFormat.format(new Date(year, month - 1, day))
}

const relativeFormat = new Intl.RelativeTimeFormat('fr', { numeric: 'auto' })

/** "à l'instant", "il y a 5 minutes", "hier"… */
export function formatRelative(iso: string, now: Date = new Date()): string {
  const seconds = Math.round((new Date(iso).getTime() - now.getTime()) / 1000)
  const abs = Math.abs(seconds)
  if (abs < 45) return 'à l’instant'
  if (abs < 3600) return relativeFormat.format(Math.round(seconds / 60), 'minute')
  if (abs < 86_400) return relativeFormat.format(Math.round(seconds / 3600), 'hour')
  return relativeFormat.format(Math.round(seconds / 86_400), 'day')
}

const dateTimeFormat = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'medium', timeStyle: 'short' })

export function formatDateTime(iso: string): string {
  return dateTimeFormat.format(new Date(iso))
}
