import type { ConsentText } from '../../api/consentTexts'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Skeleton } from '../../components/Skeleton'
import type { Resource } from '../../hooks/useResource'
import './ConsentTextPanel.css'

const DATE = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' })

/**
 * Sunucudaki aydınlatma metni (A5). Metin düz yazı olarak çizilir (HTML yorumlanmaz); paragraflar boş satırla ayrılır.
 * Kutu sabit yükseklikte ve kendi içinde kayar: metin yüklenince sayfanın geri kalanı yer değiştirmez.
 */
export function ConsentTextPanel({ resource }: { resource: Resource<ConsentText | undefined> }) {
  const { data, error, loading, reload } = resource

  if (error) {
    return (
      <Alert tone="danger" title="Aydınlatma metni yüklenemedi">
        <p>{error}</p>
        <Button variant="secondary" onClick={reload}>
          Yeniden dene
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
          <h3 id="consent-text-title">{data.title}</h3>
          <div className="consent-text-body" tabIndex={0} aria-label="Aydınlatma metni">
            {data.body
              .split(/\n\s*\n/)
              .map((paragraph) => paragraph.trim())
              .filter(Boolean)
              .map((paragraph, index) => (
                <p key={index}>{paragraph}</p>
              ))}
          </div>
          <p className="consent-text-meta">
            Metin sürümü {data.version} · {DATE.format(new Date(data.publishedAt))}
          </p>
        </>
      )}
    </section>
  )
}
