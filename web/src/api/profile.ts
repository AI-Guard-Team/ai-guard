import { apiRequest } from './client'

// Yükleme S2 kartından, geri alma A3 kartından.
export const PROFILE_ENDPOINTS = {
  photo: '/api/me/photo',
  withdrawBiometricConsent: '/api/me/biometric-consent',
} as const

export const BIOMETRIC_TEXT_VERSION = 'kvkk-biometric-v1'

export function uploadReferencePhoto(photo: Blob): Promise<void> {
  const body = new FormData()
  body.append('photo', photo, photo instanceof File ? photo.name : 'kayit-fotografi.jpg')
  body.append('consent', 'true')
  body.append('consentTextVersion', BIOMETRIC_TEXT_VERSION)
  return apiRequest(PROFILE_ENDPOINTS.photo, { method: 'POST', body })
}

export function withdrawBiometricConsent(): Promise<void> {
  return apiRequest(PROFILE_ENDPOINTS.withdrawBiometricConsent, { method: 'DELETE' })
}
