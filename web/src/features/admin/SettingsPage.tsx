import { ChevronRight, FileText, ShieldAlert, Timer } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { getRiskSettings } from '../../api/admin'
import { getConsentText, type ConsentText, type ConsentType } from '../../api/consentTexts'
import { listExams } from '../../api/exams'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Modal } from '../../components/Modal'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { eventLabel } from '../../lib/eventCatalog'
import { formatDate, formatNumber } from '../../lib/format'
import './admin.css'

const CONSENT_TYPES: { type: ConsentType; label: string; hint: string }[] = [
  { type: 'Biometric', label: 'Biyometrik veri aydınlatma metni', hint: 'Kayıt fotoğrafı yüklenirken onaylanır.' },
  { type: 'Session', label: 'Sınav oturumu aydınlatma metni', hint: 'Her sınavın başında onaylanır.' },
]

const loadAll = async (signal: AbortSignal) => {
  const [biometric, session, exams, risk] = await Promise.all([
    getConsentText('Biometric', signal),
    getConsentText('Session', signal),
    listExams(signal),
    getRiskSettings(signal),
  ])
  return { texts: { Biometric: biometric, Session: session } as Record<ConsentType, ConsentText | undefined>, exams, risk }
}

/**
 * Ayarlar (Stitch 31): platformda gerçekten var olan ayarların özeti. Rıza metinleri sürümlü ve salt okunur (K-32),
 * saklama süresi sınav başına (sınav formunda), risk değerleri platform geneli (K-40). Uydurma bir ayar yok.
 */
export function SettingsPage() {
  const { data, error, reload } = useResource(loadAll, 'Ayarlar yüklenemedi.')
  const [reading, setReading] = useState<ConsentText | null>(null)

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: 'Yönetim' }]}
        title="Ayarlar"
        description="Rıza metinleri, veri saklama ve risk değerleri."
        info={
          <ul>
            <li>Rıza metinleri sürümlüdür; yayımlanan metin değiştirilemez, yeni sürüm yayımlanır.</li>
            <li>Kanıt saklama süresi her sınavın kendi ayarıdır; süre dolunca kanıtlar günlük görevle silinir.</li>
            <li>Risk değerleri bütün sınavlar için geçerlidir.</li>
          </ul>
        }
      />

      {error && !data ? (
        <Alert tone="danger" title="Ayarlar yüklenemedi">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : !data ? (
        <Skeleton height="24rem" radius="lg" />
      ) : (
        <div className="settings-grid">
          <section className="editor-card" aria-labelledby="settings-consent">
            <div className="settings-head">
              <span className="settings-icon" aria-hidden="true">
                <FileText size={20} />
              </span>
              <div>
                <h2 className="editor-card-title" id="settings-consent">
                  Rıza metinleri
                </h2>
                <p className="editor-card-sub">Adayların onayladığı güncel aydınlatma metinleri.</p>
              </div>
            </div>
            <ul className="settings-list">
              {CONSENT_TYPES.map(({ type, label, hint }) => {
                const text = data.texts[type]
                return (
                  <li key={type}>
                    <span>
                      <strong>{label}</strong>
                      <span className="admin-row-sub">
                        {text ? `Sürüm ${text.version} · ${formatDate(text.publishedAt)}` : 'Yayımlanmış metin yok'} · {hint}
                      </span>
                    </span>
                    {text ? (
                      <Button variant="secondary" onClick={() => setReading(text)}>
                        Metni oku
                      </Button>
                    ) : null}
                  </li>
                )
              })}
            </ul>
          </section>

          <section className="editor-card" aria-labelledby="settings-retention">
            <div className="settings-head">
              <span className="settings-icon" aria-hidden="true">
                <Timer size={20} />
              </span>
              <div>
                <h2 className="editor-card-title" id="settings-retention">
                  Veri saklama
                </h2>
                <p className="editor-card-sub">Saklama süresi dolan kanıt kareleri, olaylar ve yüz vektörü silinir; sonuç ve karar kalır.</p>
              </div>
            </div>
            {data.exams.length === 0 ? (
              <p className="admin-row-sub">Henüz sınav yok.</p>
            ) : (
              <ul className="settings-list">
                {data.exams.map((exam) => (
                  <li key={exam.id}>
                    <span>
                      <strong>{exam.name}</strong>
                      <span className="admin-row-sub">{exam.retentionDays} gün</span>
                    </span>
                    <Link className="settings-link" to={`/admin/exams/${exam.id}`}>
                      Değiştir
                      <ChevronRight size={16} aria-hidden="true" />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="editor-card" aria-labelledby="settings-risk">
            <div className="settings-head">
              <span className="settings-icon" aria-hidden="true">
                <ShieldAlert size={20} />
              </span>
              <div>
                <h2 className="editor-card-title" id="settings-risk">
                  Risk değerleri
                </h2>
                <p className="editor-card-sub">Oturum skoru {formatNumber(data.risk.sessionThreshold)} değerine ulaşınca oturum riskli işaretlenir. En ağır beş olay:</p>
              </div>
            </div>
            <ul className="settings-list">
              {data.risk.events.slice(0, 5).map((rule) => (
                <li key={rule.type}>
                  <span>
                    <strong>{eventLabel(rule.type)}</strong>
                  </span>
                  <span className="numeric">{formatNumber(rule.weight, 1)}</span>
                </li>
              ))}
            </ul>
            {data.exams[0] ? (
              <Link className="settings-link" to={`/admin/exams/${data.exams[0].id}/risk`}>
                Bütün olay ağırlıkları
                <ChevronRight size={16} aria-hidden="true" />
              </Link>
            ) : null}
          </section>
        </div>
      )}

      <Modal open={reading !== null} title={reading?.title ?? 'Rıza metni'} onClose={() => setReading(null)}>
        {reading ? (
          <div className="consent-reader">
            <p className="admin-row-sub">
              Sürüm {reading.version} · {formatDate(reading.publishedAt)}
            </p>
            <div className="consent-reader-body">{reading.body}</div>
          </div>
        ) : null}
      </Modal>
    </div>
  )
}
