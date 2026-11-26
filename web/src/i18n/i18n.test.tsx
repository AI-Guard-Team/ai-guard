import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ROLE_LABEL, ROLE_MENU } from '../auth/roles'
import { EVENT_LABEL } from '../lib/eventCatalog'
import { formatDate } from '../lib/format'
import { validateEmail } from '../pages/auth/passwordRules'
import { jsonResponse, mockFetch } from '../test/fetch'
import { renderApp } from '../test/renderApp'
import { getLang, setLang } from './lang'
import { defineMessages, live, pick } from './messages'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('dil altyapısı', () => {
  it('setLang <html lang> özniteliğini ve tercihi günceller', () => {
    setLang('en')
    expect(document.documentElement.lang).toBe('en')
    expect(localStorage.getItem('aiguard.lang')).toBe('en')
    expect(getLang()).toBe('en')
  })

  it('pick ve canlı sözlük geçerli dili okur', () => {
    const messages = defineMessages({ tr: { hello: 'Merhaba' }, en: { hello: 'Hello' } })
    const labels = live(messages)
    expect(pick(messages).hello).toBe('Merhaba')
    expect(labels.hello).toBe('Merhaba')
    setLang('en')
    expect(pick(messages).hello).toBe('Hello')
    expect(labels.hello).toBe('Hello')
    expect(Object.keys(labels)).toEqual(['hello'])
  })

  it('etiket eşlemeleri iki dilde aynı anahtarları taşır', () => {
    const trKeys = Object.keys(EVENT_LABEL)
    const trMenu = ROLE_MENU.Admin.map((item) => item.to)
    setLang('en')
    expect(Object.keys(EVENT_LABEL)).toEqual(trKeys)
    expect(ROLE_MENU.Admin.map((item) => item.to)).toEqual(trMenu)
    expect(ROLE_LABEL.Reviewer).toBe('Reviewer')
    expect(EVENT_LABEL.TAB_BLUR).toBe('Left the tab')
  })

  it('tarih biçimi dile göre değişir', () => {
    const date = new Date(2026, 10, 25, 10, 0)
    expect(formatDate(date)).toContain('Kasım')
    setLang('en')
    expect(formatDate(date)).toContain('November')
  })

  it('doğrulama mesajları geçerli dilde', () => {
    expect(validateEmail('')).toBe('E-posta zorunludur.')
    setLang('en')
    expect(validateEmail('')).toBe('Email is required.')
    expect(validateEmail('a'.repeat(250) + '@x.co')).toMatch(/at most 254/)
  })
})

describe('dil düğmesi', () => {
  it('giriş ekranını İngilizceye çevirir; yazılan e-posta kaybolmaz', async () => {
    mockFetch(() => jsonResponse(500))
    await renderApp('/login')

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.click(screen.getByRole('button', { name: 'Switch to English' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('aday@aiguard.local')
    expect(document.documentElement.lang).toBe('en')

    await userEvent.click(screen.getByRole('button', { name: 'Türkçeye geç' }))
    expect(screen.getByRole('heading', { level: 1, name: 'Giriş yap' })).toBeInTheDocument()
  })

  it('İngilizcede sunucunun Türkçe hata metni yerine İngilizce açıklama gösterir', async () => {
    mockFetch(() => jsonResponse(401, { title: 'Giriş başarısız', detail: 'E-posta veya şifre hatalı.' }))
    act(() => setLang('en'))
    await renderApp('/login')

    await userEvent.type(screen.getByLabelText('Email'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Password'), 'yanlis-sifre')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Incorrect email or password.')).toBeInTheDocument()
  })
})

describe('bağlantı kodu', () => {
  it('sıfırlama kodu okunduktan sonra adres çubuğundan silinir, form yine çalışır', async () => {
    const fetchMock = mockFetch(() => new Response(null, { status: 204 }))
    await renderApp('/reset-password?token=abcDEF_123-xyz')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'yeni-sifre-123')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'yeni-sifre-123')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi yenile' }))

    expect(await screen.findByRole('heading', { name: 'Şifreniz yenilendi' })).toBeInTheDocument()
    const sent = JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string)
    expect(sent).toEqual({ token: 'abcDEF_123-xyz', password: 'yeni-sifre-123' })
  })

  it('biçime uymayan kod sunucuya gönderilmez', async () => {
    const fetchMock = mockFetch(() => new Response(null, { status: 204 }))
    await renderApp('/reset-password?token=%3Cscript%3E')

    expect(screen.getByRole('heading', { name: 'Bağlantı eksik' })).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
