import { describe, it, expect, vi, beforeEach } from 'vitest'
import { fetchApi } from '../../src/api/client'

describe('fetchApi', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('returns parsed JSON on success', async () => {
    const data = { id: 1, name: 'test' }
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify(data), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    )

    const result = await fetchApi('/api/test')
    expect(result).toEqual(data)
  })

  it('throws with status and body on non-ok response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('Not Found', { status: 404 }),
    )

    await expect(fetchApi('/api/missing')).rejects.toThrow('API 404: Not Found')
  })

  it('sends Content-Type application/json header', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('{}', { status: 200 }),
    )

    await fetchApi('/api/test')

    const call = vi.mocked(fetch).mock.calls[0]
    const init = call[1] as RequestInit
    const headers = new Headers(init.headers)
    expect(headers.get('Content-Type')).toBe('application/json')
  })

  it('passes through custom init options', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('{}', { status: 200 }),
    )

    await fetchApi('/api/test', { method: 'POST', body: '{"a":1}' })

    const call = vi.mocked(fetch).mock.calls[0]
    const init = call[1] as RequestInit
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"a":1}')
  })

  it('throws on 500 server error', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('Internal Server Error', { status: 500 }),
    )

    await expect(fetchApi('/api/test')).rejects.toThrow('API 500: Internal Server Error')
  })
})
