import { describe, expect, it } from 'vitest'
import type { ExamRiskSettings } from '../../api/management'
import { hasRiskErrors, parseNumber, ruleDiffers, toRiskDraft, toRiskRequest, validateRisk } from './riskForm'

const settings: ExamRiskSettings = {
  examId: 'e1',
  sessionThreshold: 60,
  defaultSessionThreshold: 60,
  locked: false,
  events: [
    { type: 'PHONE', weight: 25, decayPerMinute: 0.02, threshold: 30, defaultWeight: 25, defaultDecayPerMinute: 0.02, defaultThreshold: 30, customized: false },
    { type: 'TAB_BLUR', weight: 4, decayPerMinute: 0.05, threshold: null, defaultWeight: 4, defaultDecayPerMinute: 0.05, defaultThreshold: null, customized: false },
  ],
}

describe('sınava özel risk formu', () => {
  it('değişmeyen formda sunucuya boş istek gider', () => {
    expect(toRiskRequest(toRiskDraft(settings), settings)).toEqual({ sessionThreshold: null, events: [] })
  })

  it('yalnızca değişen tür ve eşik gider; virgüllü sayı kabul', () => {
    const draft = toRiskDraft(settings)
    draft.sessionThreshold = '75'
    draft.rules[1].weight = '6,5'
    const request = toRiskRequest(draft, settings)

    expect(parseNumber('6,5')).toBe(6.5)
    expect(request).toEqual({ sessionThreshold: 75, events: [{ type: 'TAB_BLUR', weight: 6.5, decayPerMinute: 0.05, threshold: null }] })
    expect(ruleDiffers(draft.rules[1], settings)).toBe(true)
    expect(ruleDiffers(draft.rules[0], settings)).toBe(false)
  })

  it('sınır dışı değerleri ve boş ağırlığı yakalar', () => {
    const draft = toRiskDraft(settings)
    draft.sessionThreshold = '0'
    draft.rules[0] = { ...draft.rules[0], weight: '', decay: '9', threshold: '0' }
    const errors = validateRisk(draft)

    expect(errors.session).toBe('session')
    expect(errors.rules.PHONE).toEqual(['weight', 'decay', 'threshold'])
    expect(hasRiskErrors(validateRisk(toRiskDraft(settings)))).toBe(false)
  })
})
