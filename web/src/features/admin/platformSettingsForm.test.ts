import { describe, expect, it } from 'vitest'
import { isHttpsUrl, toRequest, validate, type PlatformDraft } from './platformSettingsForm'

const empty: PlatformDraft = { alternativeMethodText: '', supportEmail: '', supportPhone: '', agentDownloadUrlWindows: '', agentDownloadUrlMac: '' }

describe('platform ayarları formu', () => {
  it('boş alanı null, dolu alanı kırpılmış gönderir', () => {
    expect(toRequest({ ...empty, supportEmail: '  destek@kurum.edu.tr ' })).toEqual({ ...toRequest(empty), supportEmail: 'destek@kurum.edu.tr' })
    expect(toRequest(empty).alternativeMethodText).toBeNull()
  })

  it('yalnızca https bağlantı kabul eder', () => {
    expect(isHttpsUrl('https://kurum.edu.tr/ajan.exe')).toBe(true)
    expect(isHttpsUrl('http://kurum.edu.tr/ajan.exe')).toBe(false)
    expect(isHttpsUrl('javascript:alert(1)')).toBe(false)
    expect(isHttpsUrl('kurum.edu.tr')).toBe(false)
  })

  it('hatalı e-posta, telefon ve adresi alan alan bildirir', () => {
    expect(
      validate({ ...empty, supportEmail: 'destek', supportPhone: '<script>', agentDownloadUrlMac: 'http://x.test', alternativeMethodText: 'a'.repeat(4001) }),
    ).toEqual({ supportEmail: 'email', supportPhone: 'phone', agentDownloadUrlMac: 'https', alternativeMethodText: 'tooLong' })
    expect(validate({ ...empty, supportPhone: '+90 (212) 555-0190', supportEmail: 'a@b.co' })).toEqual({})
  })
})
