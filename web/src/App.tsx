import { useEffect, useState } from 'react'
import { fetchHealth } from './api/health'

type ApiState = 'loading' | 'ok' | 'error'

const labels: Record<ApiState, string> = {
  loading: 'Connexion à l’API…',
  ok: 'API connectée',
  error: 'API injoignable',
}

const dotColors: Record<ApiState, string> = {
  loading: 'bg-slate-400',
  ok: 'bg-emerald-500',
  error: 'bg-red-500',
}

export default function App() {
  const [apiState, setApiState] = useState<ApiState>('loading')

  useEffect(() => {
    const controller = new AbortController()
    fetchHealth(controller.signal)
      .then((h) => setApiState(h.status === 'ok' ? 'ok' : 'error'))
      .catch((e: unknown) => {
        if (!controller.signal.aborted) setApiState('error')
        void e
      })
    return () => controller.abort()
  }, [])

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <header className="border-b border-slate-200 px-6 py-4 dark:border-slate-800">
        <h1 className="text-xl font-semibold">TaskHub</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Toutes mes tâches, au même endroit.
        </p>
      </header>
      <main className="px-6 py-8">
        <p className="flex items-center gap-2 text-sm" role="status">
          <span className={`inline-block size-2.5 rounded-full ${dotColors[apiState]}`} aria-hidden />
          {labels[apiState]}
        </p>
      </main>
    </div>
  )
}
