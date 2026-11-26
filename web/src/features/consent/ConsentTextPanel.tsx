import type { ConsentText } from '../../api/consentTexts'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Skeleton } from '../../components/Skeleton'
import type { Resource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDate } from '../../lib/format'
import './ConsentTextPanel.css'

const messages = defineMessages({
  tr: {
    failed: 'Aydınlatma metni yüklenemedi',
    text: 'Aydınlatma metni',
    version: (version: string, date: string) => `Metin sürümü ${version} · ${date}`,
    // Türkçe arayüzde ek not gerekmez.
    turkishOnly: '',
  },
  en: {
    failed: 'The privacy notice could not be loaded',
    text: 'Privacy notice',
    version: (version: string, date: string) => `Text version ${version} · ${date}`,
    turkishOnly: 'This legal text is published in Turkish; the Turkish version is binding.',
  },
})

/**
 * Sunucudaki aydınlatma metni (A5). Metin düz yazı olarak çizilir (HTML yorumlanmaz); paragraflar boş satırla ayrılır.
 * Kutu sabit yükseklikte ve kendi içinde kayar: metin yüklenince sayfanın geri kalanı yer değiştirmez.
 */
export function ConsentTextPanel({ resource }: { resource: Resource<ConsentText | undefined> }) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { data, error, loading, reload } = resource

  if (error) {
    return (
      <Alert tone="danger" title={m.failed}>
        <p>{error}</p>
        <Button variant="secondary" onClick={reload}>
          {c.retry}
        </Button>
      </Alert>
    )
  }

  return (
    <section className="consent-text" aria-busy={loading} aria-labelledby="consent-text-title">
      {loading || !data ? (
        <>
          <Skeleton height="1.25rem" width="60%" />
          <div className="consent-text-body">
            <Skeleton height="0.875rem" />
            <Skeleton height="0.875rem" />
            <Skeleton height="0.875rem" width="80%" />
          </div>
        </>
      ) : (
        <>
          <h3 id="consent-text-title" lang="tr">
            {data.title}
          </h3>
          <div className="consent-text-body" tabIndex={0} aria-label={m.text} lang="tr">
            {data.body
              .split(/\n\s*\n/)
              .map((paragraph) => paragraph.trim())
              .filter(Boolean)
              .map((paragraph, index) => (
                <p key={index}>{paragraph}</p>
              ))}
          </div>
          <p className="consent-text-meta">
            {m.version(data.version, formatDate(data.publishedAt))}
          </p>
          {m.turkishOnly ? <p className="consent-text-meta">{m.turkishOnly}</p> : null}
        </>
      )}
    </section>
  )
}
