import { describe, expect, it } from 'vitest'
import { formatRemaining, remainingMs } from './remainingTime'

describe('kalan süre', () => {
  it('bitişi sunucu saati ve yerel geçen süreyle hesaplar', () => {
    const serverTime = '2026-11-21T10:00:00.000Z'
    const endsAt = '2026-11-21T10:12:30.000Z'

    expect(remainingMs(endsAt, serverTime, 0)).toBe(12 * 60_000 + 30_000)
    expect(remainingMs(endsAt, serverTime, 90_000)).toBe(11 * 60_000)
  })

  it('süreyi dakika ve saniye olarak yazar', () => {
    expect(formatRemaining(90_000)).toBe('1:30')
    expect(formatRemaining(1)).toBe('0:01')
    expect(formatRemaining(0)).toBe('0:00')
    expect(formatRemaining(-500)).toBe('0:00')
  })
})
