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
import { common } from '../../i18n/common'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { PHASE_LABEL, PHASE_TONE, windowPhase, type WindowPhase } from './examWindow'
import './admin.css'

type Filter = 'all' | WindowPhase

const FILTER_KEYS: Filter[] = ['all', 'open', 'upcoming', 'closed']

const messages = defineMessages({
  tr: {
    filters: { all: 'Tümü', open: 'Erişime açık', upcoming: 'Planlandı', closing: 'Son başlangıç geçti', closed: 'Sona erdi' } as Record<Filter, string>,
    failed: 'Sınavlar yüklenemedi.',
    admin: 'Yönetim',
    title: 'Sınavlar',
    description: 'Sınavları oluşturun, sorularını ve atamalarını yönetin.',
    info: [
      'Bir sınav dört adımda hazırlanır: genel bilgiler, sorular, atamalar ve risk ayarları.',
      'Aday, erişim penceresi açıkken ve son başlangıç saatinden önce sınava başlayabilir.',
      'Bir satıra tıklayınca sınavın düzenleme ekranı açılır.',
    ],
    newExam: 'Yeni sınav',
    filterBy: 'Duruma göre süz',
    search: 'Sınav ara',
    searchPlaceholder: 'Sınav adıyla ara',
    listFailed: 'Sınavlar yüklenemedi',
    emptyTitle: 'Henüz sınav yok',
    firstExam: 'İlk sınavı oluştur',
    emptyText: 'Sınav oluşturduktan sonra soru ekleyip adayları ve değerlendiricileri atayabilirsiniz.',
    noMatch: 'Eşleşen sınav yok',
    noMatchText: 'Aramayı ya da filtreyi değiştirin.',
    closes: (at: string) => `${at} kapanır`,
    noQuestions: 'Soru yok',
  },
  en: {
    filters: { all: 'All', open: 'Open', upcoming: 'Scheduled', closing: 'Latest start passed', closed: 'Ended' },
    failed: 'Exams could not be loaded.',
    admin: 'Administration',
    title: 'Exams',
    description: 'Create exams and manage their questions and assignments.',
    info: [
      'An exam is prepared in four steps: general details, questions, assignments and risk settings.',
      'Candidates can start while the access window is open and before the latest start time.',
      'Click a row to open the exam editor.',
    ],
    newExam: 'New exam',
    filterBy: 'Filter by status',
    search: 'Search exams',
    searchPlaceholder: 'Search by exam name',
    listFailed: 'Exams could not be loaded',
    emptyTitle: 'No exams yet',
    firstExam: 'Create the first exam',
    emptyText: 'After creating an exam, add questions and assign candidates and reviewers.',
    noMatch: 'No matching exams',
    noMatchText: 'Change the search or the filter.',
    closes: (at: string) => `Closes ${at}`,
    noQuestions: 'No questions',
  },
})

const load = (signal: AbortSignal) => listExams(signal)

/** Yönetici sınav listesi (Stitch 25): durum, pencere, soru sayısı; satır sınavın düzenleme ekranını açar. */
export function AdminExamsPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { data, error, loading, reload } = useResource(load, m.failed)
  const now = useNow(30_000)
  const [filter, setFilter] = useState<Filter>('all')
  const [query, setQuery] = useState('')
  // Yazarken liste ertelenmiş değerle süzülür: arama kutusu takılmaz.
  const deferredQuery = useDeferredValue(query)

  const rows = useMemo(() => {
    const locale = getLocale()
    const term = deferredQuery.trim().toLocaleLowerCase(locale)
    return (data ?? [])
      .map((exam) => ({ exam, phase: windowPhase(exam, now.getTime()) }))
      .filter(({ exam, phase }) => (filter === 'all' || phase === filter || (filter === 'open' && phase === 'closing')) && (!term || exam.name.toLocaleLowerCase(locale).includes(term)))
      .sort((a, b) => new Date(b.exam.windowStartAt).getTime() - new Date(a.exam.windowStartAt).getTime())
  }, [data, filter, deferredQuery, now])

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
        actions={
          <Link className="button button-primary button-md" to="/admin/exams/new">
            <Plus size={18} aria-hidden="true" />
            {m.newExam}
          </Link>
        }
      />

      <div className="admin-toolbar">
        <div className="chip-tabs" role="group" aria-label={m.filterBy}>
          {FILTER_KEYS.map((key) => (
            <button key={key} type="button" className="chip-tab" aria-pressed={filter === key} onClick={() => setFilter(key)}>
              {m.filters[key]}
            </button>
          ))}
        </div>
        <label className="search-field">
          <Search size={18} aria-hidden="true" />
          <span className="visually-hidden">{m.search}</span>
          <input type="search" placeholder={m.searchPlaceholder} maxLength={200} value={query} onChange={(event) => setQuery(event.target.value)} />
        </label>
      </div>

      {error ? (
        <Alert tone="danger" title={m.listFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
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
          title={m.emptyTitle}
          action={
            <Link className="button button-primary button-md" to="/admin/exams/new">
              <Plus size={18} aria-hidden="true" />
              {m.firstExam}
            </Link>
          }
        >
          {m.emptyText}
        </EmptyState>
      ) : data && rows.length === 0 ? (
        <EmptyState icon={Search} title={m.noMatch}>
          {m.noMatchText}
        </EmptyState>
      ) : (
        <ul className="admin-list">
          {rows.map(({ exam, phase }, index) => (
            <motion.li key={exam.id} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.22, delay: Math.min(index, 8) * 0.03 }}>
              <Link className="admin-row" to={`/admin/exams/${exam.id}`}>
                <span className="admin-row-main">
                  <span className="admin-row-title">{exam.name}</span>
                  <span className="admin-row-sub">
                    {EXAM_TYPE_LABEL[exam.type]} · {c.minutes(exam.durationMinutes)} · {c.questions(exam.questionCount)}
                  </span>
                </span>
                <span className="admin-row-window">
                  <span>{formatDateTime(exam.windowStartAt)}</span>
                  <span className="admin-row-sub">{m.closes(formatDateTime(exam.windowEndAt))}</span>
                </span>
                <StatusBadge tone={PHASE_TONE[phase]} dot>
                  {PHASE_LABEL[phase]}
                </StatusBadge>
                {exam.questionCount === 0 ? <StatusBadge tone="warning">{m.noQuestions}</StatusBadge> : <span />}
                <ChevronRight size={18} className="admin-row-arrow" aria-hidden="true" />
              </Link>
            </motion.li>
          ))}
        </ul>
      )}
    </div>
  )
}
