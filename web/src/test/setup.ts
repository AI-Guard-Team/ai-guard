import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import { tokenStore } from '../auth/tokenStore'

afterEach(() => {
  cleanup()
  // Belirteç deposu modül düzeyinde tutulduğu için testler arasında sızmaması adına sıfırlanır.
  tokenStore.clear()
})
