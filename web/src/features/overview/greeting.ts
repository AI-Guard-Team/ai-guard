import { defineMessages, pick } from '../../i18n/messages'

const messages = defineMessages({
  tr: { named: (name: string) => `Merhaba, ${name} 👋`, anonymous: 'Hoş geldiniz 👋' },
  en: { named: (name: string) => `Hello, ${name} 👋`, anonymous: 'Welcome 👋' },
})

/** Karşılama: ad varsa adıyla, yoksa genel. */
export const greeting = (firstName: string) => (firstName ? pick(messages).named(firstName) : pick(messages).anonymous)
