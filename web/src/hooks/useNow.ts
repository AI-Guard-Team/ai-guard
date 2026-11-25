import { useEffect, useState } from 'react'

/** Şimdiki zaman; `intervalMs`'de bir yenilenir. Sınav penceresi açılınca kart kendiliğinden "başlatılabilir"e geçer. */
export function useNow(intervalMs = 30_000): Date {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), intervalMs)
    return () => window.clearInterval(timer)
  }, [intervalMs])
  return now
}
