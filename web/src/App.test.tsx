import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { FACETS, ITEMS, page, SOURCES } from './test/fixtures'
import { callsTo, mockApi } from './test/mockApi'
import { renderApp } from './test/renderApp'

const baseRoutes = {
  'GET /api/items': page(),
  'GET /api/items/facets': FACETS,
  'GET /api/sources': SOURCES,
}

const lastItemsCall = (fetchMock: ReturnType<typeof mockApi>) => callsTo(fetchMock, '/api/items').at(-1)

const rowTitles = () =>
  within(screen.getByRole('region', { name: 'Tâches' }))
    .getAllByRole('link')
    .map((a) => a.textContent?.replace(/\(ouvre.*\)$/, '').trim())

describe('App', () => {
  it('affiche les tâches dans l’ordre du serveur avec lien externe et échéance dépassée en rouge', async () => {
    mockApi(baseRoutes)

    renderApp()

    expect(await screen.findByText('3 tâches')).toBeInTheDocument()
    expect(rowTitles()).toEqual(['Fix login bug', 'Write docs', 'Review contract'])

    const link = screen.getByRole('link', { name: /Fix login bug/ })
    expect(link).toHaveAttribute('href', 'https://example.test/1')
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')

    const overdue = document.querySelectorAll('[data-overdue]')
    expect(overdue).toHaveLength(1)
    expect(overdue[0]).toHaveTextContent('En retard')
    expect(overdue[0]).toHaveClass('text-red-600')
  })

  it('demande par défaut les tâches triées par priorité', async () => {
    const fetchMock = mockApi(baseRoutes)

    renderApp()

    await screen.findByText('3 tâches')
    expect(lastItemsCall(fetchMock)?.searchParams.get('sort')).toBe('priority')
  })

  it('affiche l’état des sources, y compris les erreurs', async () => {
    mockApi(baseRoutes)

    renderApp()

    const banner = screen.getByRole('region', { name: 'État des sources' })
    expect(await within(banner).findByText('Azure DevOps')).toBeInTheDocument()
    expect(within(banner).getByText(/HTTP 401/)).toBeInTheDocument()
  })

  it('montre une synchro en cours comme telle, pas comme un échec', async () => {
    const running = { startedAt: '2026-10-14T08:00:00Z', finishedAt: null, succeeded: false, itemCount: 0, error: null }
    mockApi({
      ...baseRoutes,
      'GET /api/sources': { isSyncing: true, sources: [{ ...SOURCES.sources[0], lastRun: running }] },
    })

    renderApp()

    const banner = screen.getByRole('region', { name: 'État des sources' })
    expect(await within(banner).findByText(/synchronisation en cours/)).toBeInTheDocument()
    expect(within(banner).queryByText(/échec|Erreur/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Synchronisation…' })).toBeDisabled()
  })

  it('filtre par statut et garde le filtre dans l’URL', async () => {
    const fetchMock = mockApi(baseRoutes)
    const user = userEvent.setup()
    renderApp()
    await screen.findByText('3 tâches')

    await user.click(screen.getByRole('button', { name: /Bloqué/ }))

    expect(screen.getByRole('button', { name: /Bloqué/ })).toHaveAttribute('aria-pressed', 'true')
    expect(window.location.search).toBe('?status=Blocked')
    await waitFor(() => expect(lastItemsCall(fetchMock)?.searchParams.getAll('status')).toEqual(['Blocked']))
  })

  it('filtre par source et par projet', async () => {
    const fetchMock = mockApi(baseRoutes)
    const user = userEvent.setup()
    renderApp()
    await screen.findByText('3 tâches')

    await user.click(screen.getByRole('button', { name: /Jira/ }))
    await user.click(screen.getByText('Projets'))
    await user.click(screen.getByRole('checkbox', { name: /Docs/ }))
    await user.click(screen.getByRole('checkbox', { name: /Mobile/ }))

    expect(window.location.search).toBe('?source=jira&project=Docs&project=Mobile')
    await waitFor(() => {
      const params = lastItemsCall(fetchMock)?.searchParams
      expect(params?.getAll('source')).toEqual(['jira'])
      expect(params?.getAll('project')).toEqual(['Docs', 'Mobile'])
    })
  })

  it('applique les filtres présents dans l’URL au chargement', async () => {
    const fetchMock = mockApi(baseRoutes)

    renderApp('?status=InProgress&q=login&sort=dueDate')

    await screen.findByText('3 tâches')
    const params = lastItemsCall(fetchMock)?.searchParams
    expect(params?.getAll('status')).toEqual(['InProgress'])
    expect(params?.get('q')).toBe('login')
    expect(params?.get('sort')).toBe('dueDate')
    expect(screen.getByRole('searchbox', { name: 'Rechercher' })).toHaveValue('login')
    expect(screen.getByRole('combobox', { name: 'Trier par' })).toHaveValue('dueDate')
  })

  it('change le tri', async () => {
    const fetchMock = mockApi(baseRoutes)
    const user = userEvent.setup()
    renderApp()
    await screen.findByText('3 tâches')

    await user.selectOptions(screen.getByRole('combobox', { name: 'Trier par' }), 'Échéance')

    expect(window.location.search).toBe('?sort=dueDate')
    await waitFor(() => expect(lastItemsCall(fetchMock)?.searchParams.get('sort')).toBe('dueDate'))
  })

  it('recherche après une courte pause de frappe', async () => {
    const fetchMock = mockApi(baseRoutes)
    const user = userEvent.setup()
    renderApp()
    await screen.findByText('3 tâches')

    await user.type(screen.getByRole('searchbox', { name: 'Rechercher' }), 'login')

    await waitFor(() => expect(lastItemsCall(fetchMock)?.searchParams.get('q')).toBe('login'))
    // One request for the whole word, not one per keystroke.
    expect(callsTo(fetchMock, '/api/items').filter((u) => u.searchParams.has('q'))).toHaveLength(1)
    expect(window.location.search).toBe('?q=login')
  })

  it('groupe par projet', async () => {
    mockApi(baseRoutes)
    const user = userEvent.setup()
    renderApp()
    await screen.findByText('3 tâches')

    await user.selectOptions(screen.getByRole('combobox', { name: 'Grouper par' }), 'Projet')

    const headings = screen.getAllByRole('heading', { level: 2 }).map((h) => h.firstChild?.textContent)
    expect(headings).toEqual(['Docs', 'Mobile', 'Web'])
  })

  it('distingue liste vide et aucun résultat pour les filtres', async () => {
    mockApi({ ...baseRoutes, 'GET /api/items': page([]) })
    const user = userEvent.setup()
    renderApp()

    expect(await screen.findByText('Aucune tâche ne vous est assignée.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /En cours/ }))
    expect(await screen.findByText('Aucune tâche ne correspond à ces filtres.')).toBeInTheDocument()

    await user.click(screen.getAllByRole('button', { name: 'Réinitialiser les filtres' })[0])
    expect(await screen.findByText('Aucune tâche ne vous est assignée.')).toBeInTheDocument()
    expect(window.location.search).toBe('')
  })

  it('affiche une erreur avec possibilité de réessayer', async () => {
    let fail = true
    mockApi({
      ...baseRoutes,
      'GET /api/items': () =>
        fail ? Response.json({ title: 'Erreur interne' }, { status: 500 }) : Response.json(page()),
    })
    const user = userEvent.setup()
    renderApp()

    expect(await screen.findByText('Impossible de charger les tâches.')).toBeInTheDocument()
    expect(screen.getByText('Erreur interne')).toBeInTheDocument()

    fail = false
    await user.click(screen.getByRole('button', { name: 'Réessayer' }))
    expect(await screen.findByText('3 tâches')).toBeInTheDocument()
  })

  it('le bouton Synchroniser lance une synchro puis recharge la liste', async () => {
    let synced = false
    const fetchMock = mockApi({
      ...baseRoutes,
      'GET /api/items': () => Response.json(synced ? page([...ITEMS, { ...ITEMS[0], id: 'jira:NEW', title: 'Nouvelle' }]) : page()),
      'POST /api/sync': () => {
        synced = true
        return Response.json({ results: [] })
      },
    })
    const user = userEvent.setup()
    renderApp()
    await screen.findByText('3 tâches')

    await user.click(screen.getByRole('button', { name: 'Synchroniser' }))

    expect(await screen.findByText('4 tâches')).toBeInTheDocument()
    expect(callsTo(fetchMock, '/api/sync', 'POST')).toHaveLength(1)
  })

  it('affiche le message d’erreur de synchro (409 déjà en cours)', async () => {
    mockApi({
      ...baseRoutes,
      'POST /api/sync': () => Response.json({ title: 'Synchronisation en cours', detail: 'Une synchronisation est déjà en cours.' }, { status: 409 }),
    })
    const user = userEvent.setup()
    renderApp()
    await screen.findByText('3 tâches')

    await user.click(screen.getByRole('button', { name: 'Synchroniser' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Une synchronisation est déjà en cours.')
  })
})
