import { useEffect, useRef, useState } from 'react'

const reducedMotion = () => typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches

/**
 * Sayının hedefe doğru akarak gelmesi (genel bakış kutuları). Yalnızca tam sayılar; ease-out ile kısa sürer ve
 * sonraki değişimlerde kaldığı yerden devam eder. Hareket azaltma tercihinde değer doğrudan gösterilir.
 */
export function useCountUp(target: number, duration = 700): number {
  const [shown, setShown] = useState(() => (reducedMotion() ? target : 0))
  const from = useRef(shown)
  const frame = useRef(0)

  useEffect(() => {
    cancelAnimationFrame(frame.current)
    if (reducedMotion() || from.current === target) {
      from.current = target
      frame.current = requestAnimationFrame(() => setShown(target))
      return () => cancelAnimationFrame(frame.current)
    }
    const start = performance.now()
    const begin = from.current
    const step = (now: number) => {
      const progress = Math.min(1, (now - start) / duration)
      const eased = 1 - (1 - progress) ** 3
      const value = Math.round(begin + (target - begin) * eased)
      from.current = value
      setShown(value)
      if (progress < 1) frame.current = requestAnimationFrame(step)
    }
    frame.current = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frame.current)
  }, [target, duration])

  return shown
}
