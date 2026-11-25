import { RefreshCw, TriangleAlert } from 'lucide-react'
import { Component, type ErrorInfo, type ReactNode } from 'react'
import './RouteErrorBoundary.css'

interface Props {
  /** Değişince (ör. sayfa adresi) hata sıfırlanır, yeni sayfa yeniden denenir. */
  resetKey?: string
  children: ReactNode
}

interface State {
  error: Error | null
  key?: string
}

/**
 * Sayfa çizilirken ya da sayfa parçası indirilirken hata olursa boş ekran yerine açıklama ve "Yenile" gösterir.
 * Yeni sürüm yayımlandığında eski parçanın bulunamaması (yüklenemeyen modül) da buraya düşer.
 */
export class RouteErrorBoundary extends Component<Props, State> {
  state: State = { error: null, key: this.props.resetKey }

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error }
  }

  static getDerivedStateFromProps(props: Props, state: State): Partial<State> | null {
    return props.resetKey !== state.key ? { error: null, key: props.resetKey } : null
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Sayfa hatası', error, info.componentStack)
  }

  render() {
    if (!this.state.error) return this.props.children
    const chunk = /dynamically imported module|Importing a module script failed|Loading chunk/i.test(this.state.error.message)
    return (
      <div className="route-error" role="alert">
        <span className="route-error-icon" aria-hidden="true">
          <TriangleAlert size={26} />
        </span>
        <h1>{chunk ? 'Sayfa yüklenemedi' : 'Bu sayfada bir sorun oluştu'}</h1>
        <p>{chunk ? 'Uygulamanın yeni bir sürümü yayımlanmış olabilir. Sayfayı yenileyince düzelir.' : 'Sayfayı yenileyip tekrar deneyin. Sorun sürerse yöneticinize haber verin.'}</p>
        <button type="button" className="button button-primary button-md" onClick={() => window.location.reload()}>
          <RefreshCw size={18} aria-hidden="true" />
          Sayfayı yenile
        </button>
      </div>
    )
  }
}
