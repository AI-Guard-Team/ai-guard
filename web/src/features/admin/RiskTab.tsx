import { getRiskSettings } from '../../api/admin'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { InfoTip } from '../../components/InfoTip'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { eventLabel } from '../../lib/eventCatalog'
import { formatNumber } from '../../lib/format'

const load = (signal: AbortSignal) => getRiskSettings(signal)

/**
 * Risk ayarları (Stitch 29; `GET /api/risk-settings`, K-41). Değerler platform geneli ve salt okunur: sunucunun
 * skor hesabında kullandığı riskconfig.json. Sınav başına düzenleme ayrı bir karar (S-30); ekranda öyle söyleniyor.
 */
export function RiskTab() {
  const { data, error, reload } = useResource(load, 'Risk ayarları yüklenemedi.')
  const maxWeight = Math.max(1, ...(data?.events.map((item) => item.weight) ?? [1]))

  return (
    <div className="editor-card">
      <div className="editor-card-head">
        <div>
          <h2 className="editor-card-title">
            Risk ayarları{' '}
            <InfoTip title="Skor nasıl hesaplanır?">
              <ul>
                <li>Her olay türünün bir ağırlığı var; olay süresi uzadıkça ve aynı tür tekrarlandıkça katkı artar.</li>
                <li>Bazı olayların katkısı zamanla üstel olarak azalır (dakika başına katsayı).</li>
                <li>Oturum skoru eşiği geçince oturum "riskli" işaretlenir ve değerlendiriciye düşer. Kararı her zaman insan verir.</li>
              </ul>
            </InfoTip>
          </h2>
          <p className="editor-card-sub">Bu değerler bütün sınavlar için geçerli ve sunucu yapılandırmasından okunuyor.</p>
        </div>
      </div>

      <Alert tone="info">
        Ayarlar şu an yalnızca görüntülenebilir; değiştirmek için sunucudaki risk yapılandırması güncellenir. Sınav başına ayar henüz yok.
      </Alert>

      {error && !data ? (
        <Alert tone="danger">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : !data ? (
        <Skeleton height="20rem" />
      ) : (
        <>
          <dl className="risk-facts">
            <div>
              <dt>Oturum eşiği</dt>
              <dd>{formatNumber(data.sessionThreshold)}</dd>
            </div>
            <div>
              <dt>Tekrar başına artış</dt>
              <dd>+{formatNumber(data.repetitionStep, 2)}</dd>
            </div>
            <div>
              <dt>En fazla tekrar çarpanı</dt>
              <dd>×{formatNumber(data.maxRepetitionMultiplier, 1)}</dd>
            </div>
          </dl>

          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Olay</th>
                <th scope="col">Ağırlık</th>
                <th scope="col">Zamanla azalma</th>
                <th scope="col">Tek başına riskli eşiği</th>
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
                  <td className="numeric">{rule.decayPerMinute > 0 ? `${formatNumber(rule.decayPerMinute, 3)} / dk` : 'Yok'}</td>
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
