import { ChevronRight, FileText, ShieldAlert, Timer } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { getRiskSettings } from '../../api/admin'
import { getConsentText, type ConsentText, type ConsentType } from '../../api/consentTexts'
import { listExams } from '../../api/exams'
import { getPlatformSettings } from '../../api/management'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Modal } from '../../components/Modal'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { eventLabel } from '../../lib/eventCatalog'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDate, formatNumber } from '../../lib/format'
import { PlatformSettingsCard } from './PlatformSettingsCard'
import './admin.css'

const CONSENT_TYPES: ConsentType[] = ['Biometric', 'Session']

const messages = defineMessages({
  tr: {
    consent: {
      Biometric: { label: 'Biyometrik veri aydınlatma metni', hint: 'Kayıt fotoğrafı yüklenirken onaylanır.' },
      Session: { label: 'Sınav oturumu aydınlatma metni', hint: 'Her sınavın başında onaylanır.' },
    } as Record<ConsentType, { label: string; hint: string }>,
    failed: 'Ayarlar yüklenemedi.',
    admin: 'Yönetim',
    title: 'Ayarlar',
    description: 'Aday iletişimi, ajan indirme adresi, rıza metinleri, veri saklama ve risk değerleri.',
    info: [
      'Rıza metinleri sürümlüdür; yayımlanan metin değiştirilemez, yeni sürüm yayımlanır.',
      'Kanıt saklama süresi her sınavın kendi ayarıdır; süre dolunca kanıtlar günlük görevle silinir.',
      'Risk değerleri bütün sınavların varsayılanıdır; bir sınav ilk oturumu açılana kadar kendi değerlerini kullanabilir.',
    ],
    loadFailed: 'Ayarlar yüklenemedi',
    consentTitle: 'Rıza metinleri',
    consentSub: 'Adayların onayladığı güncel aydınlatma metinleri.',
    version: (version: string, date: string) => `Sürüm ${version} · ${date}`,
    noText: 'Yayımlanmış metin yok',
    read: 'Metni oku',
    retention: 'Veri saklama',
    retentionSub: 'Saklama süresi dolan kanıt kareleri, olaylar ve yüz vektörü silinir; sonuç ve karar kalır.',
    noExams: 'Henüz sınav yok.',
    days: (n: number) => `${n} gün`,
    change: 'Değiştir',
    risk: 'Risk değerleri',
    riskSub: (threshold: string) => `Varsayılan: oturum skoru ${threshold} değerine ulaşınca oturum riskli işaretlenir. En ağır beş olay:`,
    allWeights: 'Bütün olay ağırlıkları',
    consentText: 'Rıza metni',
  },
  en: {
    consent: {
      Biometric: { label: 'Biometric data privacy notice', hint: 'Accepted when uploading a reference photo.' },
      Session: { label: 'Exam session privacy notice', hint: 'Accepted at the start of every exam.' },
    },
    failed: 'Settings could not be loaded.',
    admin: 'Administration',
    title: 'Settings',
    description: 'Candidate contact, agent download address, consent texts, data retention and risk values.',
    info: [
      'Consent texts are versioned; a published text cannot be changed, a new version is published instead.',
      'Evidence retention is set per exam; when it ends, evidence is deleted by a daily job.',
      'Risk values are the default for every exam; an exam can use its own values until its first session opens.',
    ],
    loadFailed: 'Settings could not be loaded',
    consentTitle: 'Consent texts',
    consentSub: 'The current privacy notices candidates accept.',
    version: (version: string, date: string) => `Version ${version} · ${date}`,
    noText: 'No published text',
    read: 'Read text',
    retention: 'Data retention',
    retentionSub: 'When the period ends, evidence frames, events and face vectors are deleted; the result and decision remain.',
    noExams: 'No exams yet.',
    days: (n: number) => `${n} ${n === 1 ? 'day' : 'days'}`,
    change: 'Change',
    risk: 'Risk values',
    riskSub: (threshold: string) => `Default: a session is marked risky when its score reaches ${threshold}. The five heaviest events:`,
    allWeights: 'All event weights',
    consentText: 'Consent text',
  },
})

const loadAll = async (signal: AbortSignal) => {
  const [biometric, session, exams, risk, platform] = await Promise.all([
    getConsentText('Biometric', signal),
    getConsentText('Session', signal),
    listExams(signal),
    getRiskSettings(signal),
    getPlatformSettings(signal),
  ])
  return { texts: { Biometric: biometric, Session: session } as Record<ConsentType, ConsentText | undefined>, exams, risk, platform }
}

/**
 * Ayarlar (Stitch 31): platformda gerçekten var olan ayarlar. Aday iletişimi ve ajan adresi düzenlenebilir (K-49),
 * rıza metinleri sürümlü ve salt okunur (K-32), saklama süresi sınav başına (sınav formunda), risk değerleri
 * platform varsayılanı; sınava özel değerler sınavın risk sekmesinde (K-49). Uydurma bir ayar yok.
 */
export function SettingsPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { data, error, reload } = useResource(loadAll, m.failed)
  const [reading, setReading] = useState<ConsentText | null>(null)

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: m.admin }]}
        title={m.title}
        description={m.description}
        info={
          <ul>
            {m.info.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        }
      />

      {error && !data ? (
        <Alert tone="danger" title={m.loadFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : !data ? (
        <Skeleton height="24rem" radius="lg" />
      ) : (
        <div className="settings-grid">
          <PlatformSettingsCard settings={data.platform} onSaved={reload} />

          <section className="editor-card" aria-labelledby="settings-consent">
            <div className="settings-head">
              <span className="settings-icon" aria-hidden="true">
                <FileText size={20} />
              </span>
              <div>
                <h2 className="editor-card-title" id="settings-consent">
                  {m.consentTitle}
                </h2>
                <p className="editor-card-sub">{m.consentSub}</p>
              </div>
            </div>
            <ul className="settings-list">
              {CONSENT_TYPES.map((type) => {
                const text = data.texts[type]
                const { label, hint } = m.consent[type]
                return (
                  <li key={type}>
                    <span>
                      <strong>{label}</strong>
                      <span className="admin-row-sub">
                        {text ? m.version(text.version, formatDate(text.publishedAt)) : m.noText} · {hint}
                      </span>
                    </span>
                    {text ? (
                      <Button variant="secondary" onClick={() => setReading(text)}>
                        {m.read}
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
                  {m.retention}
                </h2>
                <p className="editor-card-sub">{m.retentionSub}</p>
              </div>
            </div>
            {data.exams.length === 0 ? (
              <p className="admin-row-sub">{m.noExams}</p>
            ) : (
              <ul className="settings-list">
                {data.exams.map((exam) => (
                  <li key={exam.id}>
                    <span>
                      <strong>{exam.name}</strong>
                      <span className="admin-row-sub">{m.days(exam.retentionDays)}</span>
                    </span>
                    <Link className="settings-link" to={`/admin/exams/${exam.id}`}>
                      {m.change}
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
                  {m.risk}
                </h2>
                <p className="editor-card-sub">{m.riskSub(formatNumber(data.risk.sessionThreshold))}</p>
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
                {m.allWeights}
                <ChevronRight size={16} aria-hidden="true" />
              </Link>
            ) : null}
          </section>
        </div>
      )}

      <Modal open={reading !== null} title={reading?.title ?? m.consentText} onClose={() => setReading(null)}>
        {reading ? (
          <div className="consent-reader">
            <p className="admin-row-sub">{m.version(reading.version, formatDate(reading.publishedAt))}</p>
            <div className="consent-reader-body" lang="tr">
              {reading.body}
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  )
}
