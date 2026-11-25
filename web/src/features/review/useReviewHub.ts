import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef } from 'react'
import { tokenStore } from '../../auth/tokenStore'
import type { SessionFlag } from '../../api/review'

/** Sunucunun "SessionReadyForReview" mesajı (A4, K-31). */
export interface SessionReadyMessage {
  sessionId: string
  examId: string
  riskScore: number
  flags: SessionFlag[]
  endedAt: string
}

/**
 * İnceleme bildirimleri (`/hubs/review`). Sunucu değerlendiriciyi yalnızca atandığı sınavların grubuna ekliyor;
 * sayfa açılınca bağlanır, kapanınca kopar. Bağlantı kurulamazsa sessizce vazgeçilir: liste yine elle yenilenebilir.
 */
export function useReviewHub(onReady: (message: SessionReadyMessage) => void): void {
  const handler = useRef(onReady)
  useEffect(() => {
    handler.current = onReady
  })

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/review', { accessTokenFactory: () => tokenStore.get()?.accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build()
    connection.on('SessionReadyForReview', (message: SessionReadyMessage) => handler.current(message))
    const started = connection.start().catch(() => undefined)
    return () => {
      void started.then(() => connection.stop())
    }
  }, [])
}
