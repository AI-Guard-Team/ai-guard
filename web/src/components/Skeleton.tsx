import './Skeleton.css'

interface SkeletonProps {
  /** CSS yüksekliği; içerik geldiğinde aynı yeri kaplasın diye gerçek bileşenin yüksekliği verilir. */
  height?: string
  width?: string
  radius?: 'sm' | 'md' | 'lg'
  className?: string
}

/** Yükleme yer tutucusu. Ekran okuyucudan gizli; yükleme durumu çağıran yerde aria-busy ile bildirilir. */
export function Skeleton({ height = '1rem', width = '100%', radius = 'md', className }: SkeletonProps) {
  return <span className={['skeleton', `skeleton-${radius}`, className].filter(Boolean).join(' ')} style={{ height, width }} aria-hidden="true" />
}
