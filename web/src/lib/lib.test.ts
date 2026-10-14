import { describe, expect, it } from 'vitest'
import { item } from '../test/fixtures'
import { formatDueDate, formatRelative, isOverdue, sourceLabel, todayIso } from './format'
import { groupItems } from './grouping'

describe('groupItems', () => {
  const items = [
    item({ id: 'jira:1', project: 'Web' }),
    item({ id: 'azuredevops:2', project: 'API' }),
    item({ id: 'azuredevops:3', project: 'Web' }),
  ]

  it('renvoie un seul groupe sans groupage, dans l’ordre reçu', () => {
    const groups = groupItems(items, 'none')

    expect(groups).toHaveLength(1)
    expect(groups[0].items.map((i) => i.id)).toEqual(['jira:1', 'azuredevops:2', 'azuredevops:3'])
  })

  it('groupe par projet, groupes triés par nom et tri du serveur conservé dans chaque groupe', () => {
    const groups = groupItems(items, 'project')

    expect(groups.map((g) => [g.label, g.items.map((i) => i.id)])).toEqual([
      ['API', ['azuredevops:2']],
      ['Web', ['jira:1', 'azuredevops:3']],
    ])
  })

  it('groupe par source avec un libellé lisible', () => {
    expect(groupItems(items, 'source', sourceLabel).map((g) => g.label)).toEqual(['Azure DevOps', 'Jira'])
  })
})

describe('dates', () => {
  it('une échéance passée est en retard, pas celle du jour', () => {
    expect(isOverdue('2026-10-13', '2026-10-14')).toBe(true)
    expect(isOverdue('2026-10-14', '2026-10-14')).toBe(false)
    expect(isOverdue(null, '2026-10-14')).toBe(false)
  })

  it('todayIso utilise la date locale', () => {
    expect(todayIso(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05')
  })

  it('formate une échéance sans décalage de fuseau', () => {
    expect(formatDueDate('2026-10-01')).toMatch(/^1 oct\.? 2026$/)
  })

  it('formate un temps relatif', () => {
    const now = new Date('2026-10-14T10:00:00Z')
    expect(formatRelative('2026-10-14T09:59:50Z', now)).toBe('à l’instant')
    expect(formatRelative('2026-10-14T09:55:00Z', now)).toBe('il y a 5 minutes')
    expect(formatRelative('2026-10-14T07:00:00Z', now)).toBe('il y a 3 heures')
  })
})
