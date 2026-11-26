import './AuthOrbit.css'

/**
 * Giriş panelinin arka planı (Şeyma'nın S1 "koyu cam" tasarımındaki parlayan halkalar): iki köşede iç içe halkalar,
 * üzerlerinde yavaşça dönen ışık yayları ve nefes alan yumuşak parıltı. Yalnızca süs; ekran okuyucudan gizli.
 * Dönüş yalnızca transform ile (kompozitörde, ana iş parçacığını yormaz); hareket azaltma tercihinde durur.
 */
export function AuthOrbit({ soft = false }: { soft?: boolean }) {
  return (
    <div className={soft ? 'auth-orbit auth-orbit-soft' : 'auth-orbit'} aria-hidden="true">
      <div className="orbit orbit-top">
        <span className="orbit-ring orbit-ring-1" />
        <span className="orbit-ring orbit-ring-2" />
        <span className="orbit-ring orbit-ring-3" />
        <span className="orbit-glow" />
      </div>
      <div className="orbit orbit-bottom">
        <span className="orbit-ring orbit-ring-1" />
        <span className="orbit-ring orbit-ring-2" />
        <span className="orbit-glow" />
      </div>
    </div>
  )
}
