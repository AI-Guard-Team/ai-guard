import { describe, expect, it } from 'vitest'
import type { ExamSummary, MySession } from '../../api/exams'
import { examStatus } from './examStatus'

const NOW = new Date('2026-11-25T12:00:00Z')

function exam(overrides: Partial<ExamSummary> = {}, session?: Partial<MySession>): ExamSummary {
  return {
    id: 'sinav-1',
    name: 'Genel Yetenek',
    type: 'GeneralAptitude',
    durationMinutes: 60,
    windowStartAt: '2026-11-25T09:00:00Z',
    windowEndAt: '2026-11-25T18:00:00Z',
    latestStartAt: '2026-11-25T17:00:00Z',
    retentionDays: 30,
    questionCount: 10,
    attemptUsed: false,
    mySession: session ? { id: 'oturum-1', state: 'ConsentPending', ...session } : null,
    ...overrides,
  }
}

describe('examStatus', () => {
  it('pencere açık ve fotoğraf varsa başlatılabilir', () => {
    expect(examStatus(exam(), true, NOW)).toMatchObject({ kind: 'ready', action: { kind: 'start' } })
  })

  it('fotoğraf yoksa başlatmak yerine fotoğraf yüklemeye yönlendirir (FR-04)', () => {
    expect(examStatus(exam(), false, NOW)).toMatchObject({ kind: 'needs-photo', action: { kind: 'upload-photo' } })
  })

  it('pencere açılmadan başlatılamaz', () => {
    expect(examStatus(exam({ windowStartAt: '2026-11-26T09:00:00Z' }), true, NOW)).toMatchObject({ kind: 'upcoming', action: { kind: 'none' } })
  })

  it('son başlangıçtan sonra kapanır', () => {
    expect(examStatus(exam({ latestStartAt: '2026-11-25T11:00:00Z' }), true, NOW)).toMatchObject({ kind: 'closed' })
  })

  it('hazırlıktaki oturuma sihirbazdan devam edilir', () => {
    expect(examStatus(exam({}, { state: 'AgentPairing' }), true, NOW)).toMatchObject({
      kind: 'preparing',
      action: { kind: 'resume-preflight', sessionId: 'oturum-1' },
    })
  })

  it.each(['InProgress', 'PermissionPending', 'Disconnected'] as const)('%s oturumunda sınava dönülür, pencere kapansa da', (state) => {
    const status = examStatus(exam({ latestStartAt: '2026-11-25T10:00:00Z' }, { state }), true, NOW)
    expect(status).toMatchObject({ kind: 'running', action: { kind: 'resume-exam', sessionId: 'oturum-1' } })
  })

  it.each(['Completed', 'Decided'] as const)('%s oturumu teslim edildi görünür ve sonuç açıklanmaz', (state) => {
    const status = examStatus(exam({ attemptUsed: true }, { state, endedAt: '2026-11-25T11:30:00Z' }), true, NOW)
    expect(status.kind).toBe('submitted')
    expect(status.note).toMatch(/^Teslim edildi/)
    expect(JSON.stringify(status)).not.toMatch(/karar|ihlal|temiz/i)
  })

  it('rıza reddiyle iptal edilen oturum hakkı tüketmez; yeniden başlatılabilir (FR-10)', () => {
    expect(examStatus(exam({}, { state: 'Cancelled' }), true, NOW)).toMatchObject({ kind: 'ready', label: 'Yeniden başlatılabilir' })
  })

  it('hakkı tüketen iptal yeniden başlatılamaz (FR-05)', () => {
    expect(examStatus(exam({ attemptUsed: true }, { state: 'Cancelled' }), true, NOW)).toMatchObject({ kind: 'used', action: { kind: 'none' } })
  })
})
