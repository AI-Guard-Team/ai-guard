import { describe, expect, it } from 'vitest'
import { makeAccessToken } from '../test/tokens'
import { decodeAccessToken } from './jwt'

describe('decodeAccessToken', () => {
  it('kısa talep adlarından kullanıcıyı okur', () => {
    const token = makeAccessToken({ sub: '42', role: 'Reviewer', email: 'a@b.c', name: 'Şeyma Çağlar' })

    expect(decodeAccessToken(token)).toEqual({ id: '42', role: 'Reviewer', email: 'a@b.c', name: 'Şeyma Çağlar' })
  })

  it('ASP.NET uzun URI talep adlarını da okur', () => {
    const token = makeAccessToken({
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': '7',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin',
    })

    expect(decodeAccessToken(token)).toMatchObject({ id: '7', role: 'Admin' })
  })

  it('tanımsız rolde null döner', () => {
    expect(decodeAccessToken(makeAccessToken({ sub: '1', role: 'Proctor' }))).toBeNull()
  })

  it('bozuk belirteçte null döner', () => {
    expect(decodeAccessToken('bozuk')).toBeNull()
    expect(decodeAccessToken('a.!!!.c')).toBeNull()
  })
})
