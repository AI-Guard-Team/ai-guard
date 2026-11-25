import { PageHeader } from '../components/PageHeader'
import { Panel } from '../components/Panel'

// Rol ana sayfalarının yer tutucusu; içerikleri sonraki ekran PR'larında (Stitch 7, 18, 23) doldurulacak.
export function RoleHomePage({ title }: { title: string }) {
  return (
    <>
      <PageHeader title={title} />
      <Panel>
        <p className="field-hint">Bu bölüm hazırlanıyor. İçerik sonraki sürümde eklenecek.</p>
      </Panel>
    </>
  )
}
