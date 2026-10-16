// Minimal fake of the two Azure DevOps endpoints TaskHub calls (WIQL + work items batch),
// so the end-to-end tests run against the real Docker image with deterministic data and no network.
import { createServer } from 'node:http'

const port = Number(process.env.MOCK_ADO_PORT ?? 4010)

const day = 24 * 60 * 60 * 1000
const iso = (offsetDays) => new Date(Date.now() + offsetDays * day).toISOString()

const initialItems = () => [
  { id: 1, project: 'Alpha', state: 'Active', title: 'Fix login redirect loop', priority: 1, due: iso(-2) },
  { id: 2, project: 'Alpha', state: 'New', title: 'Write onboarding guide', priority: 3, due: null },
  { id: 3, project: 'Beta', state: 'Active', title: 'Migrate billing jobs', priority: 2, due: iso(5) },
]

let items = initialItems()

const toWorkItem = (i) => ({
  id: i.id,
  rev: 1,
  fields: {
    'System.TeamProject': i.project,
    'System.State': i.state,
    'System.Title': i.title,
    'System.ChangedDate': iso(-1),
    'Microsoft.VSTS.Common.Priority': i.priority,
    ...(i.due ? { 'Microsoft.VSTS.Scheduling.DueDate': i.due } : {}),
  },
  url: `http://127.0.0.1:${port}/_apis/wit/workItems/${i.id}`,
})

const send = (res, status, body) => {
  res.writeHead(status, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify(body))
}

const readJson = async (req) => {
  const chunks = []
  for await (const chunk of req) chunks.push(chunk)
  const text = Buffer.concat(chunks).toString('utf8')
  return text ? JSON.parse(text) : {}
}

createServer(async (req, res) => {
  const { pathname } = new URL(req.url ?? '/', `http://127.0.0.1:${port}`)

  if (req.method === 'GET' && pathname === '/__health') return send(res, 200, { status: 'ok' })

  // Test control: restore the initial items, or add one (the sync test then proves a new item shows up).
  if (req.method === 'POST' && pathname === '/__reset') {
    items = initialItems()
    return send(res, 200, { count: items.length })
  }
  if (req.method === 'POST' && pathname === '/__items') {
    const item = await readJson(req)
    items.push({ state: 'New', priority: 2, due: null, ...item })
    return send(res, 200, { count: items.length })
  }

  if (req.method === 'POST' && pathname.endsWith('/_apis/wit/wiql')) {
    return send(res, 200, { workItems: items.map((i) => ({ id: i.id })) })
  }
  if (req.method === 'POST' && pathname.endsWith('/_apis/wit/workitemsbatch')) {
    const { ids = [] } = await readJson(req)
    const value = items.filter((i) => ids.includes(i.id)).map(toWorkItem)
    return send(res, 200, { count: value.length, value })
  }

  send(res, 404, { message: `Unexpected ${req.method} ${pathname}` })
}).listen(port, '127.0.0.1', () => console.log(`Mock Azure DevOps listening on ${port}`))
