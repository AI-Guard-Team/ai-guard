import { getRiskSettings } from '../../api/admin'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { InfoTip } from '../../components/InfoTip'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { eventLabel } from '../../lib/eventCatalog'
import { formatNumber } from '../../lib/format'

const load = (signal: AbortSignal) => getRiskSettings(signal)

const messages = defineMessages({
  tr: {
    failed: 'Risk ayarları yüklenemedi.',
    title: 'Risk ayarları',
    how: 'Skor nasıl hesaplanır?',
    howItems: [
      'Her olay türünün bir ağırlığı var; olay süresi uzadıkça ve aynı tür tekrarlandıkça katkı artar.',
      'Bazı olayların katkısı zamanla üstel olarak azalır (dakika başına katsayı).',
      'Oturum skoru eşiği geçince oturum "riskli" işaretlenir ve değerlendiriciye düşer. Kararı her zaman insan verir.',
    ],
    sub: 'Bu değerler bütün sınavlar için geçerli ve sunucu yapılandırmasından okunuyor.',
    readOnly: 'Ayarlar şu an yalnızca görüntülenebilir; değiştirmek için sunucudaki risk yapılandırması güncellenir. Sınav başına ayar henüz yok.',
    threshold: 'Oturum eşiği',
    step: 'Tekrar başına artış',
    maxMultiplier: 'En fazla tekrar çarpanı',
    event: 'Olay',
    weight: 'Ağırlık',
    decay: 'Zamanla azalma',
    single: 'Tek başına riskli eşiği',
    perMinute: (value: string) => `${value} / dk`,
    none: 'Yok',
  },
  en: {
    failed: 'Risk settings could not be loaded.',
    title: 'Risk settings',
    how: 'How is the score calculated?',
    howItems: [
      'Each event type has a weight; its contribution grows as the event lasts longer and the same type repeats.',
      'The contribution of some events decays exponentially over time (a per-minute factor).',
      'When the session score passes the threshold, the session is marked "risky" and goes to a reviewer. A person always makes the decision.',
    ],
    sub: 'These values apply to every exam and are read from the server configuration.',
    readOnly: 'The settings are read-only for now; they are changed by updating the risk configuration on the server. There are no per-exam settings yet.',
    threshold: 'Session threshold',
    step: 'Increase per repetition',
    maxMultiplier: 'Maximum repetition multiplier',
    event: 'Event',
    weight: 'Weight',
    decay: 'Decay over time',
    single: 'Risky on its own at',
    perMinute: (value: string) => `${value} / min`,
    none: 'None',
  },
})

/**
 * Risk ayarları (Stitch 29; `GET /api/risk-settings`, K-41). Değerler platform geneli ve salt okunur: sunucunun
 * skor hesabında kullandığı riskconfig.json. Sınav başına düzenleme ayrı bir karar (S-30); ekranda öyle söyleniyor.
 */
export function RiskTab() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { data, error, reload } = useResource(load, m.failed)
  const maxWeight = Math.max(1, ...(data?.events.map((item) => item.weight) ?? [1]))

  return (
    <div className="editor-card">
      <div className="editor-card-head">
        <div>
          <h2 className="editor-card-title">
            {m.title}{' '}
            <InfoTip title={m.how}>
              <ul>
                {m.howItems.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </InfoTip>
          </h2>
          <p className="editor-card-sub">{m.sub}</p>
        </div>
      </div>

      <Alert tone="info">{m.readOnly}</Alert>

      {error && !data ? (
        <Alert tone="danger">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : !data ? (
        <Skeleton height="20rem" />
      ) : (
        <>
          <dl className="risk-facts">
            <div>
              <dt>{m.threshold}</dt>
              <dd>{formatNumber(data.sessionThreshold)}</dd>
            </div>
            <div>
              <dt>{m.step}</dt>
              <dd>+{formatNumber(data.repetitionStep, 2)}</dd>
            </div>
            <div>
              <dt>{m.maxMultiplier}</dt>
              <dd>×{formatNumber(data.maxRepetitionMultiplier, 1)}</dd>
            </div>
          </dl>

          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">{m.event}</th>
                <th scope="col">{m.weight}</th>
                <th scope="col">{m.decay}</th>
                <th scope="col">{m.single}</th>
              </tr>
            </thead>
            <tbody>
              {data.events.map((rule) => (
                <tr key={rule.type}>
                  <td>
                    <strong>{eventLabel(rule.type)}</strong>
                  </td>
                  <td>
                    <span className="weight-cell">
                      <span className="weight-bar" aria-hidden="true">
                        <span style={{ width: `${(rule.weight / maxWeight) * 100}%` }} />
                      </span>
                      <span className="numeric">{formatNumber(rule.weight, 1)}</span>
                    </span>
                  </td>
                  <td className="numeric">{rule.decayPerMinute > 0 ? m.perMinute(formatNumber(rule.decayPerMinute, 3)) : m.none}</td>
                  <td className="numeric">{rule.threshold !== null ? formatNumber(rule.threshold) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  )
}
