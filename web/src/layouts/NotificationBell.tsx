import { Bell, CalendarClock, Camera, ClipboardCheck, FileQuestion, MailWarning, PlayCircle, UserPlus, type LucideIcon } from 'lucide-react'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useNavigate } from 'react-router'
import type { NotificationItem, NotificationKind } from '../api/management'
import { defineMessages, useMessages } from '../i18n/messages'
import { formatDateTime } from '../lib/format'
import { notificationTarget } from './notificationTarget'
import type { Notifications } from './useNotifications'

const messages = defineMessages({
  tr: {
    title: 'Bildirimler',
    open: (n: number) => (n ? `Bildirimler, ${n} okunmamış` : 'Bildirimler'),
    markAll: 'Tümünü okundu say',
    empty: 'Yeni bildirim yok.',
    failed: 'Bildirimler şu an yüklenemiyor.',
    text: {
      SessionAwaitingReview: (i: NotificationItem) => `${i.candidateName ?? 'Bir aday'} dosyası kararınızı bekliyor · ${i.examName ?? ''}`,
      ExamAwaitingReview: (i: NotificationItem) => `${i.examName ?? 'Sınav'}: ${i.count ?? 0} dosya karar bekliyor`,
      ExamWithoutQuestions: (i: NotificationItem) => `${i.examName ?? 'Sınav'} yaklaşıyor ama sorusu yok`,
      ExamWithoutCandidates: (i: NotificationItem) => `${i.examName ?? 'Sınav'} yaklaşıyor ama aday atanmamış`,
      InvitesExpired: (i: NotificationItem) => `${i.count ?? 0} davetin süresi doldu`,
      ExamOpensSoon: (i: NotificationItem) => `${i.examName ?? 'Sınav'} yakında açılıyor`,
      ExamOpenNow: (i: NotificationItem) => `${i.examName ?? 'Sınav'} şu an açık; başlayabilirsiniz`,
      PhotoMissing: () => 'Sınava girmeden önce kayıt fotoğrafınızı yükleyin',
    } as Record<NotificationKind, (item: NotificationItem) => string>,
  },
  en: {
    title: 'Notifications',
    open: (n: number) => (n ? `Notifications, ${n} unread` : 'Notifications'),
    markAll: 'Mark all as read',
    empty: 'No new notifications.',
    failed: 'Notifications cannot be loaded right now.',
    text: {
      SessionAwaitingReview: (i: NotificationItem) => `${i.candidateName ?? 'A candidate'}’s file is waiting for your decision · ${i.examName ?? ''}`,
      ExamAwaitingReview: (i: NotificationItem) => `${i.examName ?? 'Exam'}: ${i.count ?? 0} ${i.count === 1 ? 'file is' : 'files are'} waiting for a decision`,
      ExamWithoutQuestions: (i: NotificationItem) => `${i.examName ?? 'An exam'} is coming up but has no questions`,
      ExamWithoutCandidates: (i: NotificationItem) => `${i.examName ?? 'An exam'} is coming up but has no candidates`,
      InvitesExpired: (i: NotificationItem) => `${i.count ?? 0} ${i.count === 1 ? 'invitation has' : 'invitations have'} expired`,
      ExamOpensSoon: (i: NotificationItem) => `${i.examName ?? 'An exam'} opens soon`,
      ExamOpenNow: (i: NotificationItem) => `${i.examName ?? 'An exam'} is open now; you can start`,
      PhotoMissing: () => 'Upload your reference photo before taking an exam',
    },
  },
})

const ICON: Record<NotificationKind, LucideIcon> = {
  SessionAwaitingReview: ClipboardCheck,
  ExamAwaitingReview: ClipboardCheck,
  ExamWithoutQuestions: FileQuestion,
  ExamWithoutCandidates: UserPlus,
  InvitesExpired: MailWarning,
  ExamOpensSoon: CalendarClock,
  ExamOpenNow: PlayCircle,
  PhotoMissing: Camera,
}

/**
 * Kabuktaki bildirim zili (K-49). Panel `document.body`'ye açılıyor (yan menü taşırmasın); Esc ve dışarı tıklama
 * kapatır, odak düğmeye döner. Okunmamış sayı rozette ve ekran okuyucu etiketinde.
 */
export function NotificationBell({ notifications, collapsed }: { notifications: Notifications; collapsed: boolean }) {
  const m = useMessages(messages)
  const navigate = useNavigate()
  const reduce = useReducedMotion()
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState<{ left: number; bottom: number } | null>(null)
  const button = useRef<HTMLButtonElement>(null)
  const panel = useRef<HTMLDivElement>(null)
  const panelId = useId()
  const { items, unread, isRead, markAllRead, markRead, failed } = notifications

  useLayoutEffect(() => {
    if (!open || !button.current) return
    const place = () => {
      const rect = button.current!.getBoundingClientRect()
      const width = Math.min(360, window.innerWidth - 24)
      const left = rect.right + 12 + width > window.innerWidth ? Math.max(12, window.innerWidth - width - 12) : rect.right + 12
      setPosition({ left, bottom: Math.max(12, window.innerHeight - rect.bottom) })
    }
    place()
    window.addEventListener('resize', place)
    return () => window.removeEventListener('resize', place)
  }, [open])

  useEffect(() => {
    if (!open) return
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        button.current?.focus()
      }
    }
    const onPointer = (event: PointerEvent) => {
      const target = event.target as Node
      if (!panel.current?.contains(target) && !button.current?.contains(target)) setOpen(false)
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointer)
    panel.current?.focus()
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointer)
    }
  }, [open])

  const go = (item: NotificationItem) => {
    markRead(item.id)
    setOpen(false)
    navigate(notificationTarget(item))
  }

  return (
    <>
      <button
        ref={button}
        type="button"
        className="app-help app-bell"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        aria-label={m.open(unread)}
        title={collapsed ? m.title : undefined}
        onClick={() => setOpen((value) => !value)}
      >
        <span className="app-bell-icon">
          <Bell size={18} aria-hidden="true" />
          {unread > 0 ? (
            <motion.span key={unread} className="app-bell-badge" aria-hidden="true" initial={reduce ? false : { scale: 0.4 }} animate={{ scale: 1 }}>
              {unread > 9 ? '9+' : unread}
            </motion.span>
          ) : null}
        </span>
        <span className="app-nav-label">{m.title}</span>
      </button>

      {createPortal(
        <AnimatePresence>
          {open && position ? (
            <motion.div
              ref={panel}
              id={panelId}
              role="dialog"
              aria-label={m.title}
              tabIndex={-1}
              className="bell-panel"
              style={{ left: position.left, bottom: position.bottom }}
              initial={reduce ? { opacity: 0 } : { opacity: 0, y: 8, scale: 0.98 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={reduce ? { opacity: 0 } : { opacity: 0, y: 6, scale: 0.98 }}
              transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
            >
              <div className="bell-panel-head">
                <strong>{m.title}</strong>
                {unread > 0 ? (
                  <button type="button" className="bell-mark" onClick={markAllRead}>
                    {m.markAll}
                  </button>
                ) : null}
              </div>
              {items.length === 0 ? (
                <p className="bell-empty">{failed ? m.failed : m.empty}</p>
              ) : (
                <ul className="bell-list">
                  {items.map((item) => {
                    const Icon = ICON[item.kind]
                    return (
                      <li key={item.id}>
                        <button type="button" className={['bell-item', !isRead(item.id) && 'is-unread'].filter(Boolean).join(' ')} onClick={() => go(item)}>
                          <span className="bell-item-icon" aria-hidden="true">
                            <Icon size={16} />
                          </span>
                          <span className="bell-item-text">
                            <span>{m.text[item.kind](item)}</span>
                            <span className="bell-item-time">{formatDateTime(item.at)}</span>
                          </span>
                        </button>
                      </li>
                    )
                  })}
                </ul>
              )}
            </motion.div>
          ) : null}
        </AnimatePresence>,
        document.body,
      )}
    </>
  )
}
