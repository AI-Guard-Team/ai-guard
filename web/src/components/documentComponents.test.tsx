import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { docNumber, formatDuration, formatStamp } from '../lib/format'
import { DocumentCard } from './DocumentCard'
import { OpticalBubble } from './OpticalBubble'
import { riskLevel } from '../lib/risk'
import { RiskScore } from './RiskScore'
import { Seal } from './Seal'
import { StatusBadge } from './StatusBadge'

describe('belge bileşenleri (K-38)', () => {
  it('mühür yazısını ekran okuyucuya söyler; boş yuvada eğim ve halka yok', () => {
    const { container, rerender } = render(<Seal label="Rıza" ring="AI-GUARD" />)
    expect(screen.getByRole('img', { name: 'Rıza' })).toHaveClass('is-stamped')
    expect(container.querySelector('textPath')).not.toBeNull()

    rerender(<Seal label="Rıza" ring="AI-GUARD" state="empty" title="Rıza: bekliyor" />)
    expect(screen.getByRole('img', { name: 'Rıza: bekliyor' })).toHaveClass('is-empty')
    expect(container.querySelector('textPath')).toBeNull()
  })

  it('mühür yalnızca istenince basılma hareketini alır', () => {
    render(<Seal label="Temiz" animate />)
    expect(screen.getByRole('img', { name: 'Temiz' })).toHaveClass('is-animated')
  })

  it('belge kartı numarayı ve başlığı gösterir', () => {
    render(<DocumentCard docNo="AG-2026-3F9A" kicker="Oturum" title="Elif Yılmaz" />)
    expect(screen.getByText('AG-2026-3F9A')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Elif Yılmaz' })).toBeInTheDocument()
  })

  it('baloncuk dolu ve bayraklı durumu sınıfla taşır', () => {
    const { container } = render(
      <OpticalBubble filled flagged>
        A
      </OpticalBubble>,
    )
    expect(container.firstChild).toHaveClass('is-filled', 'is-flagged')
  })

  it('risk düzeyi sunucunun Risky işaretinden gelir, önyüz eşik tahmin etmez', () => {
    expect(riskLevel(90, false)).toBe('low')
    expect(riskLevel(10, true)).toBe('high')
    expect(riskLevel(0, false)).toBe('none')
    render(<RiskScore score={78} risky />)
    expect(screen.getByText('78')).toBeInTheDocument()
    expect(screen.getByText('Eşik üstü')).toBeInTheDocument()
  })

  it('damga rozeti ayrı sınıf alır', () => {
    render(
      <StatusBadge tone="warning" stamp>
        Ajansız
      </StatusBadge>,
    )
    expect(screen.getByText('Ajansız')).toHaveClass('badge-stamp', 'badge-warning')
  })

  it('belge numarası kimlikten türetilir; süre ve damga biçimi', () => {
    expect(docNumber('3f9a12bc-0000-4000-8000-000000000000', '2026-11-11T16:02:00Z')).toBe('AG-2026-3F9A')
    expect(formatDuration(42 * 60 + 18)).toBe('42:18')
    expect(formatDuration(3725)).toBe('1:02:05')
    expect(formatStamp(new Date(2026, 10, 11, 19, 2))).toBe('2026-11-11 19:02')
  })
})
