import { useToday } from '../api/queries'
import { formatReason } from '../lib/format'
import { ScoreBadge, SourceBadge } from './Badges'
import { ExternalIcon } from './icons'

/** The few open items to handle first, by unified score, with why. */
export function TodayPanel() {
  const { data, isError } = useToday()

  // The main list already reports loading and errors; this panel just stays out of the way.
  if (isError || !data || data.items.length === 0) return null

  return (
    <section
      aria-labelledby="today-title"
      className="rounded-lg border border-indigo-200 bg-indigo-50/60 dark:border-indigo-900 dark:bg-indigo-950/30"
    >
      <h2 id="today-title" className="px-4 pt-3 pb-1 text-sm font-semibold text-indigo-900 dark:text-indigo-200">
        À traiter en priorité aujourd’hui
      </h2>
      <ol className="divide-y divide-indigo-100 dark:divide-indigo-900/60">
        {data.items.map((item) => (
          <li key={item.id} className="flex items-start gap-3 px-4 py-2.5">
            <ScoreBadge score={item.score} reasons={item.scoreReasons} />
            <div className="min-w-0 flex-1">
              <a
                href={item.url}
                target="_blank"
                rel="noopener noreferrer"
                className="group text-sm font-medium [overflow-wrap:anywhere] hover:text-indigo-600 hover:underline dark:hover:text-indigo-400"
              >
                {item.title}
                <ExternalIcon className="ml-1 inline size-3.5 align-[-2px] opacity-40 group-hover:opacity-100" />
              </a>
              {item.scoreReasons.length > 0 && (
                <ul aria-label="Raisons" className="mt-1 flex flex-wrap gap-1.5">
                  {item.scoreReasons.map((reason) => (
                    <li
                      key={reason.kind}
                      className={`rounded px-1.5 py-0.5 text-xs ${
                        reason.kind === 'Overdue'
                          ? 'bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300'
                          : 'bg-white text-slate-600 ring-1 ring-slate-200 ring-inset dark:bg-slate-900 dark:text-slate-300 dark:ring-slate-700'
                      }`}
                    >
                      {formatReason(reason)}
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <SourceBadge source={item.source} />
          </li>
        ))}
      </ol>
    </section>
  )
}
