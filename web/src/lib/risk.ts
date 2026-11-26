import { defineMessages, live } from '../i18n/messages'

export type RiskLevel = 'high' | 'low' | 'none'

/**
 * Skorun eşiğe göre düzeyi. Eşiği sunucu belirliyor (riskconfig.json); önyüz eşiği tahmin etmiyor,
 * sunucunun koyduğu `Risky` işaretini kullanıyor.
 */
export function riskLevel(score: number, risky: boolean): RiskLevel {
  if (risky) return 'high'
  return score > 0 ? 'low' : 'none'
}

export const RISK_LEVEL_LABEL: Record<RiskLevel, string> = live(
  defineMessages<Record<RiskLevel, string>>({
    tr: { high: 'Eşik üstü', low: 'Eşik altı', none: 'Olay yok' },
    en: { high: 'Above threshold', low: 'Below threshold', none: 'No events' },
  }),
)
