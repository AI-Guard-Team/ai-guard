import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { ScorePoint } from '../api/review'
import { formatScore, formatTime } from './labels'

const CHART_HEIGHT = 220

/** Risk skorunun zaman içindeki değişimi (azalma eğrisi dahil). Veri yoksa yerine kısa bir açıklama gösterilir. */
export function RiskChart({ points }: { points: ScorePoint[] }) {
  if (points.length === 0) return <p className="table-empty">Skor geçmişi yok.</p>

  const data = points.map((point) => ({ at: new Date(point.at).getTime(), score: point.score }))
  const last = points[points.length - 1]

  return (
    <div role="img" aria-label={`Risk skoru grafiği: ${points.length} nokta, son skor ${formatScore(last.score)}`} className="risk-chart">
      <ResponsiveContainer width="100%" height={CHART_HEIGHT} initialDimension={{ width: 480, height: CHART_HEIGHT }}>
        <LineChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid stroke="var(--color-border)" strokeDasharray="3 3" />
          <XAxis dataKey="at" type="number" scale="time" domain={['dataMin', 'dataMax']} tickFormatter={formatTime} stroke="var(--color-muted)" tick={{ fontSize: 12 }} />
          <YAxis stroke="var(--color-muted)" tick={{ fontSize: 12 }} width={40} />
          <Tooltip
            labelFormatter={(value) => formatTime(Number(value))}
            formatter={(value) => [formatScore(Number(value)), 'Skor']}
            contentStyle={{ background: 'var(--color-bg)', border: '1px solid var(--color-border)' }}
          />
          <Line type="monotone" dataKey="score" stroke="var(--color-focus)" strokeWidth={2} dot={false} isAnimationActive={false} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
