import { apiRequest } from './client'

// Yükleme S2 kartından, geri alma A3 kartından, durum UI1'den (K-35).
export const PROFILE_ENDPOINTS = {
  photo: '/api/me/photo',
  withdrawBiometricConsent: '/api/me/biometric-consent',
  biometricStatus: '/api/me/biometric',
} as const

/** `GET /api/me/biometric`: yüz şablonu var mı; vektör ya da fotoğraf dönmüyor. */
export interface BiometricStatus {
  enrolled: boolean
  enrolledAt?: string | null
  consentTextVersion?: string | null
}

export async function getBiometricStatus(signal?: AbortSignal): Promise<BiometricStatus> {
  return (await apiRequest<BiometricStatus>(PROFILE_ENDPOINTS.biometricStatus, { signal })) ?? { enrolled: false }
}

/** `textVersion`: ekranda gösterilen biyometrik rıza metninin sürümü (`GET /api/consent-texts/Biometric`). */
export function uploadReferencePhoto(photo: Blob, textVersion: string): Promise<void> {
  const body = new FormData()
  body.append('photo', photo, photo instanceof File ? photo.name : 'kayit-fotografi.jpg')
  body.append('consent', 'true')
  body.append('consentTextVersion', textVersion)
  return apiRequest(PROFILE_ENDPOINTS.photo, { method: 'POST', body })
}

export function withdrawBiometricConsent(): Promise<void> {
  return apiRequest(PROFILE_ENDPOINTS.withdrawBiometricConsent, { method: 'DELETE' })
}
