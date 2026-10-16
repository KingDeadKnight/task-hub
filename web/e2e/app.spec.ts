import { expect, test } from '@playwright/test'

const MOCK = 'http://127.0.0.1:4010'

test.beforeEach(async ({ request }) => {
  await request.post(`${MOCK}/__reset`)
  // Bring the app back in line with the mock: items from a previous test must not leak.
  await request.post('/api/sync')
})

test('serves the app and its assets (no blank page)', async ({ page, request }) => {
  const failures: string[] = []
  page.on('pageerror', (e) => failures.push(e.message))
  page.on('console', (m) => m.type() === 'error' && failures.push(m.text()))

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'TaskHub' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Tâches' }).getByText('Fix login redirect loop')).toBeVisible()

  // Regression: the SPA fallback once answered asset requests with index.html.
  const assets = await page.evaluate(() =>
    [...document.querySelectorAll<HTMLScriptElement | HTMLLinkElement>('script[src], link[rel=stylesheet]')].map(
      (e) => (e as HTMLScriptElement).src || (e as HTMLLinkElement).href,
    ),
  )
  expect(assets.length).toBeGreaterThan(0)
  for (const url of assets) {
    const response = await request.get(url)
    expect(response.ok(), url).toBe(true)
    expect(response.headers()['content-type'], url).not.toContain('text/html')
  }

  expect(failures).toEqual([])
})

test('shows the synchronized items and the source status', async ({ page }) => {
  await page.goto('/')
  const list = page.getByRole('region', { name: 'Tâches' })
  await expect(list.getByText('Write onboarding guide')).toBeVisible()
  await expect(list.getByText('Migrate billing jobs')).toBeVisible()
  await expect(page.getByRole('region', { name: 'État des sources' })).toContainText(/azure/i)
})

test('filters by search text and keeps the filter in the URL', async ({ page }) => {
  await page.goto('/')
  const list = page.getByRole('region', { name: 'Tâches' })
  await expect(list.getByText('Migrate billing jobs')).toBeVisible()

  await page.getByRole('searchbox', { name: 'Rechercher' }).fill('billing')

  await expect(list.getByText('Migrate billing jobs')).toBeVisible()
  await expect(list.getByText('Fix login redirect loop')).toHaveCount(0)
  await expect(page).toHaveURL(/q=billing/)
})

test('manual synchronization brings in a new item', async ({ page, request }) => {
  await page.goto('/')
  const list = page.getByRole('region', { name: 'Tâches' })
  await expect(list.getByText('Fix login redirect loop')).toBeVisible()

  await request.post(`${MOCK}/__items`, { data: { id: 4, project: 'Beta', title: 'Added after startup' } })
  await expect(list.getByText('Added after startup')).toHaveCount(0)

  await page.getByRole('button', { name: 'Synchroniser' }).click()

  await expect(list.getByText('Added after startup')).toBeVisible()
})
