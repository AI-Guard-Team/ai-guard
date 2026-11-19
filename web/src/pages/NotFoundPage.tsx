import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <main className="standalone">
      <section className="card">
        <h1>Sayfa bulunamadı</h1>
        <p>Aradığınız sayfa mevcut değil veya taşınmış olabilir.</p>
        <Link to="/">Ana sayfaya dön</Link>
      </section>
    </main>
  )
}
