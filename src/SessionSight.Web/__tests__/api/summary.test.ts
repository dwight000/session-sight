import { describe, it, expect, vi, beforeEach } from 'vitest'
import { getPracticeSummary } from '../../src/api/summary'
import * as client from '../../src/api/client'

describe('summary API', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('getPracticeSummary builds URL with encodeURIComponent on both params', async () => {
    const spy = vi.spyOn(client, 'fetchApi').mockResolvedValue({})

    await getPracticeSummary('2025-01-01', '2025-01-31')

    expect(spy).toHaveBeenCalledWith(
      '/api/summary/practice?startDate=2025-01-01&endDate=2025-01-31',
    )
  })

  it('getPracticeSummary encodes special characters in date params', async () => {
    const spy = vi.spyOn(client, 'fetchApi').mockResolvedValue({})

    await getPracticeSummary('2025/01/01 00:00', '2025/01/31 23:59')

    const calledUrl = spy.mock.calls[0][0]
    expect(calledUrl).toContain('startDate=2025%2F01%2F01%2000%3A00')
    expect(calledUrl).toContain('endDate=2025%2F01%2F31%2023%3A59')
  })
})
