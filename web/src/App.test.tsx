import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App', () => {
  it('indique que l’API est connectée quand /api/health répond ok', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(Response.json({ status: 'ok' }))

    render(<App />)

    expect(await screen.findByText('API connectée')).toBeInTheDocument()
  })

  it('indique une erreur quand l’API ne répond pas', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 503 }))

    render(<App />)

    expect(await screen.findByText('API injoignable')).toBeInTheDocument()
  })
})
