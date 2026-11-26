import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { NotificationItem, PlatformSettings } from '../../api/management'
import { ToastProvider } from '../../components/ToastProvider'
import { invalidatePlatformSettings } from '../../hooks/usePlatformSettings'
import { NotificationBell } from '../../layouts/NotificationBell'
import type { Notifications } from '../../layouts/useNotifications'
import { jsonResponse, mockFetch } from '../../test/fetch'
import { CancelledNotice } from '../preflight/CancelledNotice'
import { PlatformSettingsCard } from './PlatformSettingsCard'

afterEach(() => {
  vi.unstubAllGlobals()
  invalidatePlatformSettings()
})

const settings: PlatformSettings = {
  alternativeMethodText: 'Gözetmenli salon sınavı için koordinatörlüğe yazın.\n<b>kalın değil</b>',
  supportEmail: 'sinav-destek@kurum.edu.tr',
  supportPhone: '+90 (212) 555 0190',
  agentDownloadUrlWindows: 'https://kurum.edu.tr/ajan.exe',
  agentDownloadUrlMac: null,
  updatedAt: '2026-11-26T10:00:00Z',
}

describe('alternatif sınav yöntemi (Stitch 17)', () => {
  it('yöneticinin metnini düz metin olarak ve iletişim bağlantılarıyla gösterir', async () => {
    mockFetch((url) => (url === '/api/platform-settings' ? jsonResponse(200, settings) : jsonResponse(404)))
    render(
      <MemoryRouter>
        <CancelledNotice />
      </MemoryRouter>,
    )

    expect(await screen.findByText(/Gözetmenli salon sınavı/)).toBeInTheDocument()
    expect(screen.getByText(/<b>kalın değil<\/b>/)).toBeInTheDocument() // HTML olarak yorumlanmadı
    expect(screen.getByRole('link', { name: /sinav-destek@kurum.edu.tr/ })).toHaveAttribute('href', 'mailto:sinav-destek@kurum.edu.tr')
    expect(screen.getByRole('link', { name: /555 0190/ })).toHaveAttribute('href', 'tel:+902125550190')
  })

  it('ayar yoksa genel yönlendirme gösterir', async () => {
    mockFetch(() => jsonResponse(500))
    render(
      <MemoryRouter>
        <CancelledNotice />
      </MemoryRouter>,
    )

    expect(await screen.findByText(/kurumunuzun sınav sorumlusuna başvurun/)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /e-posta gönder/ })).not.toBeInTheDocument()
  })
})

describe('platform ayarları kartı', () => {
  it('https olmayan adresi ve hatalı telefonu göndermeden işaretler, düzeltilince kaydeder', async () => {
    const fetchMock = mockFetch(() => new Response(null, { status: 204 }))
    const onSaved = vi.fn()
    render(
      <ToastProvider>
        <PlatformSettingsCard settings={{ ...settings, agentDownloadUrlWindows: null }} onSaved={onSaved} />
      </ToastProvider>,
    )

    const windows = screen.getByLabelText('Ajan indirme adresi (Windows)')
    await userEvent.type(windows, 'javascript:alert(1)')
    await userEvent.clear(screen.getByLabelText('Destek telefonu'))
    await userEvent.type(screen.getByLabelText('Destek telefonu'), '<script>')
    await userEvent.click(screen.getByRole('button', { name: 'Kaydet' }))

    expect(screen.getByText('https:// ile başlayan geçerli bir adres girin.')).toBeInTheDocument()
    expect(screen.getByText(/Yalnızca rakam, boşluk/)).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()

    await userEvent.clear(windows)
    await userEvent.type(windows, 'https://kurum.edu.tr/ajan.exe')
    await userEvent.clear(screen.getByLabelText('Destek telefonu'))
    await userEvent.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    const body = JSON.parse(fetchMock.mock.calls[0][1]!.body as string) as Record<string, unknown>
    expect(body).toMatchObject({ agentDownloadUrlWindows: 'https://kurum.edu.tr/ajan.exe', supportPhone: null })
  })
})

describe('bildirim zili', () => {
  const item: NotificationItem = {
    id: 'review:s1',
    kind: 'SessionAwaitingReview',
    at: '2026-11-26T10:00:00Z',
    examId: 'e1',
    examName: 'Güz Genel Yetenek',
    sessionId: 's1',
    candidateName: 'Ada Yılmaz',
    count: null,
  }

  const notifications = (overrides: Partial<Notifications> = {}): Notifications => ({
    items: [item],
    unread: 1,
    isRead: () => false,
    markAllRead: vi.fn(),
    markRead: vi.fn(),
    failed: false,
    ...overrides,
  })

  it('okunmamış sayısını söyler, bildirime tıklayınca dosyaya gider ve okundu sayar', async () => {
    const state = notifications()
    render(
      <MemoryRouter initialEntries={['/overview']}>
        <Routes>
          <Route path="/overview" element={<NotificationBell notifications={state} collapsed={false} />} />
          <Route path="/review/sessions/:id" element={<p>Dosya açıldı</p>} />
        </Routes>
      </MemoryRouter>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Bildirimler, 1 okunmamış' }))
    expect(screen.getByRole('dialog', { name: 'Bildirimler' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Ada Yılmaz dosyası kararınızı bekliyor/ }))

    expect(state.markRead).toHaveBeenCalledWith('review:s1')
    expect(await screen.findByText('Dosya açıldı')).toBeInTheDocument()
  })

  it('Esc ile kapanır; bildirim yoksa boş durum', async () => {
    render(
      <MemoryRouter>
        <NotificationBell notifications={notifications({ items: [], unread: 0 })} collapsed={false} />
      </MemoryRouter>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Bildirimler' }))
    expect(screen.getByText('Yeni bildirim yok.')).toBeInTheDocument()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })
})
