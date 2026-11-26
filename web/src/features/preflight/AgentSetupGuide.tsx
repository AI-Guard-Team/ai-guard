import { Download, MonitorCheck, MousePointerClick, RefreshCw } from 'lucide-react'
import { AGENT_DOWNLOAD_URL } from '../../api/agentPairing'
import { usePlatformSettings } from '../../hooks/usePlatformSettings'
import { isHttpsUrl } from '../../lib/safeUrl'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    download: 'İndirin',
    downloadLink: 'Güvenlik ajanını indir',
    fromInstitution: 'Kurumunuzun verdiği bağlantıdan',
    downloadText: 'dosyasını indirin (yaklaşık 45 MB). Yönetici izni gerekmez, kurulum yapılmaz.',
    run: 'Çalıştırın',
    runText:
      'Dosyaya çift tıklayın. Windows “bilgisayarınızı korudu” derse Ek bilgi’ye, ardından Yine de çalıştır’a basın. Açılan pencerede “AI-Guard güvenlik ajanı çalışıyor” yazısını görünce pencereyi kapatmayın.',
    pair: 'Eşleştirin',
    pairText: 'Bu sayfada “Ajanla eşleştir”e basın; kod yazmanız gerekmez. Tarayıcı “bu cihazdaki diğer uygulamalara erişim” izni sorarsa İzin ver’i seçin.',
    mac: 'macOS kullanıyorsunuz. macOS ajanının kurulum adımlarını kurumunuzun sınav sorumlusundan alın; eşleştirme bu sayfadan aynı şekilde yapılır.',
    macLink: 'macOS kullanıyorsunuz: yukarıdaki bağlantı macOS ajanını indirir; eşleştirme bu sayfadan aynı şekilde yapılır.',
    help: (contact: string) => `Sorun yaşarsanız: ${contact}`,
  },
  en: {
    download: 'Download',
    downloadLink: 'Download the security agent',
    fromInstitution: 'From the link your institution gave you,',
    downloadText: 'download the file (about 45 MB). No administrator rights and no installation are needed.',
    run: 'Run',
    runText:
      'Double-click the file. If Windows says it “protected your PC”, click More info, then Run anyway. Keep the window open once it shows “AI-Guard security agent is running”.',
    pair: 'Pair',
    pairText: 'Press “Pair with agent” on this page; you do not need to type a code. If the browser asks for access to other apps on this device, choose Allow.',
    mac: 'You are using macOS. Ask your institution’s exam coordinator for the macOS agent setup steps; pairing works the same way from this page.',
    macLink: 'You are using macOS: the link above downloads the macOS agent; pairing works the same way from this page.',
    help: (contact: string) => `If you run into trouble: ${contact}`,
  },
})

function isMac(): boolean {
  return typeof navigator !== 'undefined' && /Mac/i.test(navigator.userAgent)
}

/**
 * Ajanın kurulum adımları (Stitch ekran 10, docs/ajan-kurulum-windows.md). İndirme adresi yöneticinin platform
 * ayarından (K-49; işletim sistemine göre Windows ya da macOS); ayarlanmadıysa "kurumunuzun verdiği bağlantı".
 */
export function AgentSetupGuide() {
  const m = useMessages(messages)
  const mac = isMac()
  const settings = usePlatformSettings()
  const configured = mac ? settings?.agentDownloadUrlMac : settings?.agentDownloadUrlWindows
  const downloadUrl = isHttpsUrl(configured) ? configured : AGENT_DOWNLOAD_URL
  const contact = [settings?.supportEmail, settings?.supportPhone].filter(Boolean).join(' · ')
  return (
    <div className="agent-guide">
      <ol className="agent-steps">
        <li>
          <Download size={20} aria-hidden="true" />
          <span>
            <strong>{m.download}</strong>
            <span>
              {downloadUrl ? (
                <a href={downloadUrl} rel="noopener noreferrer" download>
                  {m.downloadLink}
                </a>
              ) : (
                m.fromInstitution
              )}{' '}
              {mac ? null : <code>aiguard-agent.exe</code>} {m.downloadText}
            </span>
          </span>
        </li>
        <li>
          <MousePointerClick size={20} aria-hidden="true" />
          <span>
            <strong>{m.run}</strong>
            <span>{m.runText}</span>
          </span>
        </li>
        <li>
          <RefreshCw size={20} aria-hidden="true" />
          <span>
            <strong>{m.pair}</strong>
            <span>{m.pairText}</span>
          </span>
        </li>
      </ol>
      {mac ? (
        <p className="agent-os-note">
          <MonitorCheck size={16} aria-hidden="true" />
          {downloadUrl ? m.macLink : m.mac}
        </p>
      ) : null}
      {contact ? <p className="agent-os-note">{m.help(contact)}</p> : null}
    </div>
  )
}
