import { formatScore } from './labels'

interface ScoreBarProps {
  score: number
  risky: boolean
  /** Çubuğun tam dolduğu skor; listedeki en yüksek skor verilir (eşik yapılandırmada, önyüze açık değil). */
  max: number
}

/** Skor çubuğu. Renk tek başına anlam taşımaz: sayı ve "Riskli" / "Riskli değil" metni her zaman yanında (renk körlüğü). */
export function ScoreBar({ score, risky, max }: ScoreBarProps) {
  const percent = max > 0 ? Math.min(100, Math.max(0, (score / max) * 100)) : 0
  const label = risky ? 'Riskli' : 'Riskli değil'

  return (
    <div className="score-bar">
      <div className="score-bar-track" aria-hidden="true">
        <div className={`score-bar-fill ${risky ? 'is-risky' : 'is-ok'}`} style={{ width: `${percent}%` }} />
      </div>
      <span>
        {formatScore(score)} · {label}
      </span>
    </div>
  )
}
