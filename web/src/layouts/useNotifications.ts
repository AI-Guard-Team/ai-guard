import { useCallback, useEffect, useMemo, useState } from 'react'
import { listNotifications, type NotificationItem } from '../api/management'

// Bildirim zili (K-49). Sunucu bildirimleri her istekte güncel veriden türetiyor; "okundu" bilgisi tarayıcıda,
// kullanıcıya özel anahtarla. Sekme görünürken dakikada bir yenileniyor.

const POLL_MS = 60_000
const readKey = (userId: string) => `aiguard.notifications.read.${userId}`

function readStored(userId: string): Set<string> {
  try {
    const value = JSON.parse(localStorage.getItem(readKey(userId)) ?? '[]') as unknown
    return new Set(Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : [])
  } catch {
    return new Set()
  }
}

function writeStored(userId: string, ids: Set<string>) {
  try {
    localStorage.setItem(readKey(userId), JSON.stringify([...ids]))
  } catch {
    // Depolama kapalıysa okundu bilgisi yalnızca bu sayfada kalır.
  }
}

export interface Notifications {
  items: NotificationItem[]
  unread: number
  isRead: (id: string) => boolean
  markAllRead: () => void
  markRead: (id: string) => void
  failed: boolean
}

export function useNotifications(userId: string | undefined): Notifications {
  const [items, setItems] = useState<NotificationItem[]>([])
  const [failed, setFailed] = useState(false)
  const [read, setRead] = useState<Set<string>>(() => (userId ? readStored(userId) : new Set()))

  useEffect(() => {
    if (!userId) return
    let active = true
    let controller: AbortController | null = null
    const load = () => {
      if (document.visibilityState === 'hidden') return
      controller?.abort()
      controller = new AbortController()
      listNotifications(controller.signal).then(
        (rows) => {
          if (!active) return
          setItems(rows)
          setFailed(false)
          // Artık gelmeyen bildirimlerin "okundu" kaydı tutulmaz; depolama büyümesin.
          setRead((current) => {
            const kept = new Set(rows.map((row) => row.id).filter((id) => current.has(id)))
            writeStored(userId, kept)
            return kept
          })
        },
        (error: unknown) => {
          if (active && !(error instanceof DOMException && error.name === 'AbortError')) setFailed(true)
        },
      )
    }
    load()
    const timer = window.setInterval(load, POLL_MS)
    document.addEventListener('visibilitychange', load)
    return () => {
      active = false
      controller?.abort()
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', load)
    }
  }, [userId])

  const update = useCallback(
    (next: Set<string>) => {
      setRead(next)
      if (userId) writeStored(userId, next)
    },
    [userId],
  )

  return useMemo(
    () => ({
      items,
      failed,
      unread: items.filter((item) => !read.has(item.id)).length,
      isRead: (id: string) => read.has(id),
      markAllRead: () => update(new Set(items.map((item) => item.id))),
      markRead: (id: string) => update(new Set([...read, id])),
    }),
    [items, failed, read, update],
  )
}
