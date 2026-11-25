import { formatNumber } from '../lib/format'
import { RISK_LEVEL_LABEL, riskLevel } from '../lib/risk'
import './RiskScore.css'

interface RiskScoreProps {
  score: number
  risky: boolean
  size?: 'sm' | 'md' | 'xl'
  /** Skorun altında düzey yazısı. */
  showLabel?: boolean
}

/** Risk skoru: büyük mono rakam + düzey. Renk yanında her zaman yazı var. */
export function RiskScore({ score, risky, size = 'md', showLabel = true }: RiskScoreProps) {
  const level = riskLevel(score, risky)
  return (
    <span className={`risk-score risk-score-${size} risk-${level}`}>
      <span className="risk-score-value numeric">{formatNumber(score)}</span>
      {showLabel ? <span className="risk-score-label">{RISK_LEVEL_LABEL[level]}</span> : null}
    </span>
  )
}
