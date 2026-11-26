import { Download, MonitorCheck, MousePointerClick, RefreshCw } from 'lucide-react'
import { AGENT_DOWNLOAD_URL } from '../../api/agentPairing'

function isMac(): boolean {
  return typeof navigator !== 'undefined' && /Mac/i.test(navigator.userAgent)
}

/**
 * Ajanın kurulum adımları (Stitch ekran 10, docs/ajan-kurulum-windows.md). İndirme adresi henüz yayında değil
 * (K-25); adres gelince `AGENT_DOWNLOAD_URL` doldurulur ve düğme kendiliğinden bağlantıya dönüşür.
 */
export function AgentSetupGuide() {
  const mac = isMac()
  return (
    <div className="agent-guide">
      <ol className="agent-steps">
        <li>
          <Download size={20} aria-hidden="true" />
          <span>
            <strong>İndirin</strong>
            <span>
              {AGENT_DOWNLOAD_URL ? (
                <a href={AGENT_DOWNLOAD_URL} rel="noopener">
                  Güvenlik ajanını indir
                </a>
              ) : (
                'Kurumunuzun verdiği bağlantıdan'
              )}{' '}
              <code>aiguard-agent.exe</code> dosyasını indirin (yaklaşık 45 MB). Yönetici izni gerekmez, kurulum yapılmaz.
            </span>
          </span>
        </li>
        <li>
          <MousePointerClick size={20} aria-hidden="true" />
          <span>
            <strong>Çalıştırın</strong>
            <span>
              Dosyaya çift tıklayın. Windows “bilgisayarınızı korudu” derse Ek bilgi’ye, ardından Yine de çalıştır’a basın. Açılan pencerede “AI-Guard güvenlik
              ajanı çalışıyor” yazısını görünce pencereyi kapatmayın.
            </span>
          </span>
        </li>
        <li>
          <RefreshCw size={20} aria-hidden="true" />
          <span>
            <strong>Eşleştirin</strong>
            <span>
              Bu sayfada “Ajanla eşleştir”e basın; kod yazmanız gerekmez. Tarayıcı “bu cihazdaki diğer uygulamalara erişim” izni sorarsa İzin ver’i seçin.
            </span>
          </span>
        </li>
      </ol>
      {mac ? (
        <p className="agent-os-note">
          <MonitorCheck size={16} aria-hidden="true" />
          macOS kullanıyorsunuz. macOS ajanının kurulum adımlarını kurumunuzun sınav sorumlusundan alın; eşleştirme bu sayfadan aynı şekilde yapılır.
        </p>
      ) : null}
    </div>
  )
}
