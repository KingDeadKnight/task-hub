import { useSources, useSync } from '../api/queries'
import { useTheme, type ThemePreference } from '../theme/useTheme'
import { MonitorIcon, MoonIcon, RefreshIcon, SunIcon } from './icons'

const THEME_LABELS: Record<ThemePreference, string> = {
  system: 'Thème : système',
  light: 'Thème : clair',
  dark: 'Thème : sombre',
}

function ThemeToggle() {
  const { preference, cycle } = useTheme()
  const Icon = preference === 'light' ? SunIcon : preference === 'dark' ? MoonIcon : MonitorIcon
  return (
    <button
      type="button"
      onClick={cycle}
      title={THEME_LABELS[preference]}
      aria-label={THEME_LABELS[preference]}
      className="rounded-md p-2 text-slate-600 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-indigo-500 dark:text-slate-300 dark:hover:bg-slate-800"
    >
      <Icon className="size-5" />
    </button>
  )
}

function SyncButton() {
  const sync = useSync()
  const sources = useSources()
  const busy = sync.isPending || sources.data?.isSyncing === true

  return (
    <div className="flex items-center gap-3">
      {sync.isError && (
        <p role="alert" className="max-w-48 text-right text-xs text-red-600 sm:max-w-xs sm:text-sm dark:text-red-400">
          {sync.error.message}
        </p>
      )}
      <button
        type="button"
        onClick={() => sync.mutate()}
        disabled={busy}
        className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 disabled:cursor-not-allowed disabled:opacity-60"
      >
        <RefreshIcon className={`size-4 ${busy ? 'animate-spin' : ''}`} />
        {busy ? 'Synchronisation…' : 'Synchroniser'}
      </button>
    </div>
  )
}

export function Header() {
  return (
    <header className="border-b border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="mx-auto flex max-w-5xl items-center justify-between gap-4 px-4 py-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">TaskHub</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">Toutes mes tâches, au même endroit.</p>
        </div>
        <div className="flex items-center gap-2">
          <SyncButton />
          <ThemeToggle />
        </div>
      </div>
    </header>
  )
}
