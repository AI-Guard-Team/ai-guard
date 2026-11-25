import { BookOpenCheck, ChevronRight, Plus, Search } from 'lucide-react'
import { motion } from 'motion/react'
import { useDeferredValue, useMemo, useState } from 'react'
import { Link } from 'react-router'
import { EXAM_TYPE_LABEL, listExams } from '../../api/exams'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useNow } from '../../hooks/useNow'
import { useResource } from '../../hooks/useResource'
import { formatDateTime } from '../../lib/format'
import { PHASE_LABEL, PHASE_TONE, windowPhase, type WindowPhase } from './examWindow'
import './admin.css'

type Filter = 'all' | WindowPhase

const FILTERS: { key: Filter; label: string }[] = [
  { key: 'all', label: 'Tümü' },
  { key: 'open', label: 'Erişime açık' },
  { key: 'upcoming', label: 'Planlandı' },
  { key: 'closed', label: 'Sona erdi' },
]

const load = (signal: AbortSignal) => listExams(signal)

/** Yönetici sınav listesi (Stitch 25): durum, pencere, soru sayısı; satır sınavın düzenleme ekranını açar. */
export function AdminExamsPage() {
  const { data, error, loading, reload } = useResource(load, 'Sınavlar yüklenemedi.')
  const now = useNow(30_000)
  const [filter, setFilter] = useState<Filter>('all')
  const [query, setQuery] = useState('')
  // Yazarken liste ertelenmiş değerle süzülür: arama kutusu takılmaz.
  const deferredQuery = useDeferredValue(query)

  const rows = useMemo(() => {
    const term = deferredQuery.trim().toLocaleLowerCase('tr-TR')
    return (data ?? [])
      .map((exam) => ({ exam, phase: windowPhase(exam, now.getTime()) }))
      .filter(({ exam, phase }) => (filter === 'all' || phase === filter || (filter === 'open' && phase === 'closing')) && (!term || exam.name.toLocaleLowerCase('tr-TR').includes(term)))
      .sort((a, b) => new Date(b.exam.windowStartAt).getTime() - new Date(a.exam.windowStartAt).getTime())
  }, [data, filter, deferredQuery, now])

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: 'Yönetim' }]}
        title="Sınavlar"
        description="Sınavları oluşturun, sorularını ve atamalarını yönetin."
        info={
          <ul>
            <li>Bir sınav dört adımda hazırlanır: genel bilgiler, sorular, atamalar ve risk ayarları.</li>
            <li>Aday, erişim penceresi açıkken ve son başlangıç saatinden önce sınava başlayabilir.</li>
            <li>Bir satıra tıklayınca sınavın düzenleme ekranı açılır.</li>
          </ul>
        }
        actions={
          <Link className="button button-primary button-md" to="/admin/exams/new">
            <Plus size={18} aria-hidden="true" />
            Yeni sınav
          </Link>
        }
      />

      <div className="admin-toolbar">
        <div className="chip-tabs" role="group" aria-label="Duruma göre süz">
          {FILTERS.map((item) => (
            <button key={item.key} type="button" className="chip-tab" aria-pressed={filter === item.key} onClick={() => setFilter(item.key)}>
              {item.label}
            </button>
          ))}
        </div>
        <label className="search-field">
          <Search size={18} aria-hidden="true" />
          <span className="visually-hidden">Sınav ara</span>
          <input type="search" placeholder="Sınav adıyla ara" value={query} onChange={(event) => setQuery(event.target.value)} />
        </label>
      </div>

      {error ? (
        <Alert tone="danger" title="Sınavlar yüklenemedi">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <div className="admin-list" aria-busy="true">
          <Skeleton height="4.5rem" />
          <Skeleton height="4.5rem" />
          <Skeleton height="4.5rem" />
        </div>
      ) : data && data.length === 0 ? (
        <EmptyState
          icon={BookOpenCheck}
          title="Henüz sınav yok"
          action={
            <Link className="button button-primary button-md" to="/admin/exams/new">
              <Plus size={18} aria-hidden="true" />
              İlk sınavı oluştur
            </Link>
          }
        >
          Sınav oluşturduktan sonra soru ekleyip adayları ve değerlendiricileri atayabilirsiniz.
        </EmptyState>
      ) : data && rows.length === 0 ? (
        <EmptyState icon={Search} title="Eşleşen sınav yok">
          Aramayı ya da filtreyi değiştirin.
        </EmptyState>
      ) : (
        <ul className="admin-list">
          {rows.map(({ exam, phase }, index) => (
            <motion.li key={exam.id} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.22, delay: Math.min(index, 8) * 0.03 }}>
              <Link className="admin-row" to={`/admin/exams/${exam.id}`}>
                <span className="admin-row-main">
                  <span className="admin-row-title">{exam.name}</span>
                  <span className="admin-row-sub">
                    {EXAM_TYPE_LABEL[exam.type]} · {exam.durationMinutes} dakika · {exam.questionCount} soru
                  </span>
                </span>
                <span className="admin-row-window">
                  <span>{formatDateTime(exam.windowStartAt)}</span>
                  <span className="admin-row-sub">{formatDateTime(exam.windowEndAt)} kapanır</span>
                </span>
                <StatusBadge tone={PHASE_TONE[phase]} dot>
                  {PHASE_LABEL[phase]}
                </StatusBadge>
                {exam.questionCount === 0 ? <StatusBadge tone="warning">Soru yok</StatusBadge> : <span />}
                <ChevronRight size={18} className="admin-row-arrow" aria-hidden="true" />
              </Link>
            </motion.li>
          ))}
        </ul>
      )}
    </div>
  )
}
