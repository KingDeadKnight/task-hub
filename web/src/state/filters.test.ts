import { describe, expect, it } from 'vitest'
import {
  DEFAULT_FILTERS,
  hasActiveFilters,
  parseFilters,
  serializeFilters,
  toggleValue,
  toItemsQuery,
  type Filters,
} from './filters'

describe('parseFilters', () => {
  it('renvoie les valeurs par défaut pour une URL vide', () => {
    expect(parseFilters('')).toEqual(DEFAULT_FILTERS)
  })

  it('lit les filtres multi-valués, la recherche, le tri et le groupage', () => {
    expect(
      parseFilters('?source=jira&project=Web&project=Docs&status=Blocked&status=Todo&q=%20login%20&sort=dueDate&group=project'),
    ).toEqual<Filters>({
      sources: ['jira'],
      projects: ['Web', 'Docs'],
      statuses: ['Blocked', 'Todo'],
      q: 'login',
      sort: 'dueDate',
      group: 'project',
    })
  })

  it('ignore les valeurs inconnues, vides ou en double', () => {
    const filters = parseFilters('?status=Nope&status=Todo&status=Todo&project=&sort=colour&group=planet')

    expect(filters.statuses).toEqual(['Todo'])
    expect(filters.projects).toEqual([])
    expect(filters.sort).toBe(DEFAULT_FILTERS.sort)
    expect(filters.group).toBe('none')
  })
})

describe('serializeFilters', () => {
  it('omet les valeurs par défaut', () => {
    expect(serializeFilters(DEFAULT_FILTERS)).toBe('')
  })

  it('fait l’aller-retour avec parseFilters', () => {
    const filters: Filters = {
      sources: ['azuredevops', 'jira'],
      projects: ['Wavenet - Site Web'],
      statuses: ['InProgress'],
      q: 'café & co',
      sort: '-updatedAt',
      group: 'source',
    }

    expect(parseFilters(serializeFilters(filters))).toEqual(filters)
  })
})

describe('toItemsQuery', () => {
  it('traduit les filtres en paramètres d’API, sans le groupage', () => {
    expect(toItemsQuery({ ...DEFAULT_FILTERS, statuses: ['Blocked'], q: '  bug ', sort: 'title', group: 'project' })).toEqual({
      source: undefined,
      project: undefined,
      status: ['Blocked'],
      q: 'bug',
      sort: 'title',
      pageSize: 200,
    })
  })

  it('trie par score de priorité décroissant par défaut', () => {
    expect(toItemsQuery(DEFAULT_FILTERS).sort).toBe('-score')
    expect(parseFilters('?sort=priority').sort).toBe('priority')
    expect(serializeFilters({ ...DEFAULT_FILTERS, sort: 'priority' })).toBe('?sort=priority')
  })
})

describe('hasActiveFilters / toggleValue', () => {
  it('ne considère ni le tri ni le groupage comme des filtres', () => {
    expect(hasActiveFilters({ ...DEFAULT_FILTERS, sort: 'title', group: 'source' })).toBe(false)
    expect(hasActiveFilters({ ...DEFAULT_FILTERS, q: 'x' })).toBe(true)
  })

  it('ajoute puis retire une valeur', () => {
    expect(toggleValue(['a'], 'b')).toEqual(['a', 'b'])
    expect(toggleValue(['a', 'b'], 'a')).toEqual(['b'])
  })
})
