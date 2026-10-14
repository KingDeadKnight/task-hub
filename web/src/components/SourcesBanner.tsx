import type { SourceStatus } from '../api/client'
import { useSources } from '../api/queries'
import { formatDateTime, formatRelative, sourceLabel } from '../lib/format'

function When({ iso }: { iso: string }) {
  return (
    <time dateTime={iso} title={formatDateTime(iso)}>
      {formatRelative(iso)}
    </time>
  )
}

function SourceLine({ source }: { source: SourceStatus }) {
  const run = source.lastRun
  // A run without an end is still in progress: neither a success nor a failure yet.
  const running = run !== null && run.finishedAt === null
  const failed = run !== null && !running && !run.succeeded
  const dot = run === null ? 'bg-slate-400' : running ? 'animate-pulse bg-indigo-500' : failed ? 'bg-red-500' : 'bg-emerald-500'

  return (
    <li className="flex min-w-0 items-start gap-2 text-sm">
      <span className={`mt-1.5 inline-block size-2 shrink-0 rounded-full ${dot}`} aria-hidden="true" />
      <div className="min-w-0">
        <span className="font-medium">{sourceLabel(source.name)}</span>
        <span className="text-slate-500 dark:text-slate-400">
          {' · '}
          {source.activeItemCount} élément{source.activeItemCount > 1 ? 's' : ''}
          {' · '}
          {run === null ? (
            'jamais synchronisée'
          ) : running ? (
            'synchronisation en cours…'
          ) : failed ? (
            <>
              échec <When iso={run.finishedAt ?? run.startedAt} />
            </>
          ) : (
            <>
              synchronisée <When iso={run.finishedAt ?? run.startedAt} />
            </>
          )}
        </span>
        {failed && (
          <p className="break-words text-red-700 dark:text-red-400">
            {run.error ?? 'Erreur inconnue'}
            {source.lastSuccessAt && (
              <span className="text-slate-500 dark:text-slate-400">
                {' — dernier succès '}
                <When iso={source.lastSuccessAt} />
              </span>
            )}
          </p>
        )}
      </div>
    </li>
  )
}

export function SourcesBanner() {
  const { data, isError, error } = useSources()

  let content
  if (isError && !data) {
    content = (
      <p role="alert" className="text-sm text-red-700 dark:text-red-400">
        État des sources indisponible : {error.message}
      </p>
    )
  } else if (!data) {
    content = <p className="text-sm text-slate-500 dark:text-slate-400">Chargement de l’état des sources…</p>
  } else if (data.sources.length === 0) {
    content = <p className="text-sm text-slate-500 dark:text-slate-400">Aucune source configurée.</p>
  } else {
    content = (
      <ul className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:gap-x-8">
        {data.sources.map((s) => (
          <SourceLine key={s.name} source={s} />
        ))}
      </ul>
    )
  }

  return (
    <section
      aria-label="État des sources"
      className="rounded-lg border border-slate-200 bg-white px-4 py-3 dark:border-slate-800 dark:bg-slate-900"
    >
      {content}
    </section>
  )
}
