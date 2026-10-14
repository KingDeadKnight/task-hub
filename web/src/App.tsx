import { FilterBar } from './components/FilterBar'
import { Header } from './components/Header'
import { ItemList } from './components/ItemList'
import { SourcesBanner } from './components/SourcesBanner'
import { TodayPanel } from './components/TodayPanel'
import { hasActiveFilters } from './state/filters'
import { useFilters } from './state/useFilters'

export default function App() {
  const { filters, update, reset } = useFilters()

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <Header />
      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <SourcesBanner />
        {/* While filtering, the user is looking for something specific: keep the focus on the list. */}
        {!hasActiveFilters(filters) && <TodayPanel />}
        <FilterBar filters={filters} onChange={update} onReset={reset} />
        <ItemList filters={filters} onReset={reset} />
      </main>
    </div>
  )
}
