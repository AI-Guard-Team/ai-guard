import { getLang, useLang } from './lang'

/**
 * Bir bileşenin iki dildeki metinleri. Metinler kullanıldıkları dosyanın yanında durur (modüler);
 * İngilizce sözlük Türkçeyle aynı anahtarları taşımak zorundadır, eksik ya da fazla anahtar derlemeyi durdurur.
 * Değişken içeren metinler işlevdir: `count: (n: number) => ...`.
 */
export interface Messages<T> {
  tr: T
  en: NoInfer<T>
}

export function defineMessages<T>(messages: Messages<T>): Messages<T> {
  return messages
}

/** Bileşen içinde: geçerli dilin metinleri; dil değişince yeniden çizilir. */
export function useMessages<T>(messages: Messages<T>): T {
  return messages[useLang()]
}

/** Bileşen dışındaki düz işlevlerde (etiket eşlemeleri, hata metinleri): çağrıldığı andaki dil. */
export function pick<T>(messages: Messages<T>): T {
  return messages[getLang()]
}

/**
 * Canlı sözlük: `ROLE_LABEL[role]` gibi okunan eşlemeler için. Her okuma geçerli dilin değerini verir;
 * `Object.entries` ve `Object.keys` de çalışır. Kullanan dosyalar düz bir nesneymiş gibi okumaya devam eder.
 */
export function live<T extends object>(messages: Messages<T>): T {
  return new Proxy(messages.tr, {
    get: (_target, key) => Reflect.get(pick(messages), key),
    has: (_target, key) => Reflect.has(pick(messages), key),
    ownKeys: () => Reflect.ownKeys(pick(messages)),
    getOwnPropertyDescriptor: (_target, key) => {
      const source = pick(messages)
      if (!Reflect.has(source, key)) return undefined
      return { value: Reflect.get(source, key), enumerable: true, configurable: true, writable: false }
    },
  })
}
