import { Camera, Cpu, Globe, Mic, MonitorSmartphone, type LucideIcon } from 'lucide-react'
import './AuthOrbit.css'

/**
 * Platformun izlediği sinyaller (K-13): kamera, ses, tarayıcı, cihaz (ajan), ağ. Halkanın üzerinde yörüngede döner;
 * "her skorun gerekçesi bu sinyaller" fikrinin görsel karşılığı. Yalnızca süs.
 */
const SIGNALS: { icon: LucideIcon; angle: number }[] = [
  { icon: Camera, angle: 200 },
  { icon: Mic, angle: 248 },
  { icon: MonitorSmartphone, angle: 296 },
  { icon: Cpu, angle: 344 },
  { icon: Globe, angle: 152 },
]

/**
 * Giriş panelinin arka planı (Şeyma'nın S1 "koyu cam" tasarımındaki parlayan halkalar): iki köşede iç içe halkalar,
 * üzerlerinde yavaşça dönen ışık yayları, nefes alan yumuşak parıltı ve yörüngede dönen sinyal simgeleri.
 * Yalnızca süs; ekran okuyucudan gizli. Dönüş yalnızca transform ile (kompozitörde, ana iş parçacığını yormaz);
 * hareket azaltma tercihinde durur.
 */
export function AuthOrbit({ soft = false }: { soft?: boolean }) {
  return (
    <div className={soft ? 'auth-orbit auth-orbit-soft' : 'auth-orbit'} aria-hidden="true">
      <div className="orbit orbit-top">
        <span className="orbit-ring orbit-ring-1" />
        <span className="orbit-ring orbit-ring-2" />
        <span className="orbit-ring orbit-ring-3" />
        <span className="orbit-glow" />
        {soft ? null : (
          <span className="orbit-satellites-frame">
          <span className="orbit-satellites">
            {SIGNALS.map(({ icon: Icon, angle }) => (
              // Konum: kap kendi açısına döner, simge karşı yönde döner; yörüngede gezerken simge hep dik kalır.
              <span key={angle} className="orbit-satellite" style={{ ['--angle' as string]: `${angle}deg` }}>
                <span className="orbit-satellite-upright">
                  <span className="orbit-satellite-chip">
                    <Icon size={18} strokeWidth={2} />
                  </span>
                </span>
              </span>
            ))}
          </span>
          </span>
        )}
      </div>
      <div className="orbit orbit-bottom">
        <span className="orbit-ring orbit-ring-1" />
        <span className="orbit-ring orbit-ring-2" />
        <span className="orbit-glow" />
      </div>
    </div>
  )
}
