import type { ExamRiskSettings, ExamRiskSettingsRequest } from '../../api/management'

// Sınava özel risk ayarı formu (K-49). Sınırlar sunucudakiyle aynı (ExamRiskService).

export const RISK_LIMITS = { weight: 100, decay: 5, threshold: 1000, session: 1000 } as const

export type RiskErrorCode = 'weight' | 'decay' | 'threshold' | 'session'

export interface RuleDraft {
  type: string
  weight: string
  decay: string
  threshold: string
}

export interface RiskDraft {
  sessionThreshold: string
  rules: RuleDraft[]
}

/** Alanda arayüz dilinin ondalık ayracı (Türkçede virgül); ayrıştırma ikisini de kabul ediyor. */
export function toRiskDraft(settings: ExamRiskSettings, decimal: ',' | '.' = '.'): RiskDraft {
  const text = (value: number | null) => (value === null ? '' : String(value).replace('.', decimal))
  return {
    sessionThreshold: text(settings.sessionThreshold),
    rules: settings.events.map((rule) => ({
      type: rule.type,
      weight: text(rule.weight),
      decay: text(rule.decayPerMinute),
      threshold: text(rule.threshold),
    })),
  }
}

/** "1,5" de "1.5" de kabul; boş ya da sayı değilse NaN. */
export function parseNumber(value: string): number {
  const normalized = value.trim().replace(',', '.')
  return normalized === '' ? Number.NaN : Number(normalized)
}

export function validateRisk(draft: RiskDraft): { session?: RiskErrorCode; rules: Record<string, RiskErrorCode[]> } {
  const rules: Record<string, RiskErrorCode[]> = {}
  for (const rule of draft.rules) {
    const codes: RiskErrorCode[] = []
    const weight = parseNumber(rule.weight)
    const decay = parseNumber(rule.decay)
    if (!(weight >= 0 && weight <= RISK_LIMITS.weight)) codes.push('weight')
    if (!(decay >= 0 && decay <= RISK_LIMITS.decay)) codes.push('decay')
    if (rule.threshold.trim() !== '') {
      const threshold = parseNumber(rule.threshold)
      if (!(threshold > 0 && threshold <= RISK_LIMITS.threshold)) codes.push('threshold')
    }
    if (codes.length) rules[rule.type] = codes
  }
  const session = parseNumber(draft.sessionThreshold)
  return { session: session >= 1 && session <= RISK_LIMITS.session ? undefined : 'session', rules }
}

export function hasRiskErrors(errors: ReturnType<typeof validateRisk>): boolean {
  return errors.session !== undefined || Object.keys(errors.rules).length > 0
}

/** Yalnızca varsayılandan farklı olanlar gider; varsayılana eşit olan tür ve eşik sunucuda "özel değil" sayılır. */
export function toRiskRequest(draft: RiskDraft, settings: ExamRiskSettings): ExamRiskSettingsRequest {
  const defaults = new Map(settings.events.map((rule) => [rule.type, rule]))
  const events = draft.rules.flatMap((rule) => {
    const base = defaults.get(rule.type)
    const weight = parseNumber(rule.weight)
    const decayPerMinute = parseNumber(rule.decay)
    const threshold = rule.threshold.trim() === '' ? null : parseNumber(rule.threshold)
    if (base && weight === base.defaultWeight && decayPerMinute === base.defaultDecayPerMinute && threshold === base.defaultThreshold) return []
    return [{ type: rule.type, weight, decayPerMinute, threshold }]
  })
  const session = parseNumber(draft.sessionThreshold)
  return { sessionThreshold: session === settings.defaultSessionThreshold ? null : session, events }
}

/** Satır varsayılandan farklı mı (taslakta; kaydedilmemiş değişiklik dahil). */
export function ruleDiffers(rule: RuleDraft, settings: ExamRiskSettings): boolean {
  const base = settings.events.find((item) => item.type === rule.type)
  if (!base) return false
  const threshold = rule.threshold.trim() === '' ? null : parseNumber(rule.threshold)
  return parseNumber(rule.weight) !== base.defaultWeight || parseNumber(rule.decay) !== base.defaultDecayPerMinute || threshold !== base.defaultThreshold
}
