import { describe, it, expect, vi, beforeEach } from 'vitest'
import { getReviewQueue, getReviewDetail, getReviewStats } from '../../src/api/review'
import * as client from '../../src/api/client'

describe('review API', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('getReviewQueue builds URL with no params', async () => {
    const spy = vi.spyOn(client, 'fetchApi').mockResolvedValue([])

    await getReviewQueue()

    expect(spy).toHaveBeenCalledWith('/api/review/queue')
  })

  it('getReviewQueue builds URL with status-only param', async () => {
    const spy = vi.spyOn(client, 'fetchApi').mockResolvedValue([])

    await getReviewQueue({ status: 'Pending' })

    expect(spy).toHaveBeenCalledWith('/api/review/queue?status=Pending')
  })

  it('getReviewQueue builds URL with all params', async () => {
    const spy = vi.spyOn(client, 'fetchApi').mockResolvedValue([])

    await getReviewQueue({ status: 'Approved', startDate: '2025-01-01', endDate: '2025-01-31' })

    const calledUrl = spy.mock.calls[0][0]
    expect(calledUrl).toContain('status=Approved')
    expect(calledUrl).toContain('startDate=2025-01-01')
    expect(calledUrl).toContain('endDate=2025-01-31')
  })

  it('getReviewDetail calls correct path', async () => {
    const spy = vi.spyOn(client, 'fetchApi').mockResolvedValue({})

    await getReviewDetail('sess-abc')

    expect(spy).toHaveBeenCalledWith('/api/review/session/sess-abc')
  })

  it('getReviewStats calls correct path', async () => {
    const spy = vi.spyOn(client, 'fetchApi').mockResolvedValue({})

    await getReviewStats()

    expect(spy).toHaveBeenCalledWith('/api/review/stats')
  })
})
