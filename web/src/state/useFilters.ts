import { useCallback, useEffect, useRef, useState } from 'react'
import { parseFilters, serializeFilters, type Filters } from './filters'

type UpdateOptions = {
  /** Replace the current history entry instead of adding one (e.g. while typing). */
  replace?: boolean
}

/** Filters stored in the URL query string, so a view can be bookmarked and back/forward work. */
export function useFilters() {
  const [filters, setFilters] = useState<Filters>(() => parseFilters(window.location.search))
  // Latest value for update(): history must be written outside setState updaters (run twice in StrictMode).
  const current = useRef(filters)

  useEffect(() => {
    const onPopState = () => {
      current.current = parseFilters(window.location.search)
      setFilters(current.current)
    }
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const update = useCallback((patch: Partial<Filters>, options: UpdateOptions = {}) => {
    const next = { ...current.current, ...patch }
    current.current = next
    const url = `${window.location.pathname}${serializeFilters(next)}${window.location.hash}`
    if (options.replace) window.history.replaceState(null, '', url)
    else window.history.pushState(null, '', url)
    setFilters(next)
  }, [])

  const reset = useCallback(() => update({ sources: [], projects: [], statuses: [], q: '' }), [update])

  return { filters, update, reset }
}
