/** Karşılama: ad varsa adıyla, yoksa genel. */
export const greeting = (firstName: string) => (firstName ? `Merhaba, ${firstName} 👋` : 'Hoş geldiniz 👋')
