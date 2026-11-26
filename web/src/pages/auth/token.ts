import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router'

// Davet ve sıfırlama kodu sunucuda 256 bit rastgele, base64url (K-20, K-33). Biçime uymayan değer sunucuya hiç gitmez.
const TOKEN_PATTERN = /^[A-Za-z0-9_-]{1,512}$/

export function readToken(params: URLSearchParams): string {
  const value = params.get('token')?.trim() ?? ''
  return TOKEN_PATTERN.test(value) ? value : ''
}

/**
 * Adresteki kodu bir kez okur ve adres çubuğundan siler: kod tarayıcı geçmişinde, ekran paylaşımında ya da
 * yer imlerinde kalmasın. Sayfa yenilenirse e-postadaki bağlantı yeniden açılmalı.
 */
export function useUrlToken(): string {
  const [params, setParams] = useSearchParams()
  const [token] = useState(() => readToken(params))
  useEffect(() => {
    if (params.has('token')) setParams({}, { replace: true })
  }, [params, setParams])
  return token
}
