import { ArrowRight, Camera, ClipboardCheck, Cpu, FileCheck2, Gavel, GraduationCap, Globe, Lock, Mic, MonitorSmartphone, ScrollText, ShieldCheck, Timer, UserCheck, Users } from 'lucide-react'
import { motion } from 'motion/react'
import { IconTile, type TileTone } from '../../components/IconTile'
import { Hero } from './OverviewParts'
import './overview.css'

const SOURCES: { icon: typeof Camera; tone: TileTone; title: string; text: string }[] = [
  { icon: Camera, tone: 'rose', title: 'Kamera', text: 'Yüz görünmüyor, birden fazla yüz, bakış sapması, telefon' },
  { icon: Mic, tone: 'amber', title: 'Mikrofon', text: 'Ortamda konuşma' },
  { icon: MonitorSmartphone, tone: 'sky', title: 'Tarayıcı', text: 'Sekmeden çıkış, tam ekrandan çıkış, kopyala/yapıştır' },
  { icon: Cpu, tone: 'lilac', title: 'Cihaz ajanı', text: 'Sanal makine, yasaklı uygulama, sinyal kaybı' },
  { icon: Globe, tone: 'mint', title: 'Ağ', text: 'VPN/vekil sunucu, IP değişimi, bağlantı kopması' },
]

const PRINCIPLES: { icon: typeof Camera; tone: TileTone; title: string; text: string }[] = [
  { icon: UserCheck, tone: 'indigo', title: 'Kararı insan verir', text: 'Sistem kopya kararı vermez. Riskli oturumu yetkili değerlendirici inceler; Temiz, Şüpheli ya da İhlal kararını o verir.' },
  { icon: ShieldCheck, tone: 'mint', title: 'Sürekli kayıt yok', text: 'Kamera ve ses anlık analiz edilir; yalnızca şüpheli anların kanıt karesi şifreli saklanır.' },
  { icon: FileCheck2, tone: 'lilac', title: 'Açık rıza', text: 'Aday her sınavdan önce aydınlatma metnini okuyup onaylar; biyometrik işlem yalnızca açık rızayla yapılır.' },
  { icon: Timer, tone: 'amber', title: 'Saklama süresi', text: 'Süresi dolan kanıt kareleri, olaylar ve yüz vektörü otomatik silinir; sonuç ve karar kalır.' },
  { icon: ScrollText, tone: 'rose', title: 'Denetlenebilirlik', text: 'Kararlar, rapor görüntüleme ve rol değişiklikleri değiştirilemeyen denetim kaydına yazılır.' },
  { icon: Lock, tone: 'sky', title: 'Sıfır güven', text: 'Her istek yetki ve sahiplik kontrolünden geçer; cihaz ajanının sinyalleri imzalıdır.' },
]

const ROLES: { icon: typeof Camera; tone: TileTone; title: string; text: string }[] = [
  { icon: ClipboardCheck, tone: 'sky', title: 'Aday', text: 'Atandığı sınava girer: rıza, güvenlik ajanı, kimlik doğrulama, ardından sınav.' },
  { icon: Gavel, tone: 'amber', title: 'Değerlendirici', text: 'Riskli ve işaretli dosyaları inceler, yanlış alarmları ayıklar, karar verir.' },
  { icon: Users, tone: 'lilac', title: 'Yönetici', text: 'Sınav, soru, atama ve kullanıcıları yönetir; raporları ve denetim kaydını izler.' },
]

/** Platform hakkında (K-42): amaç, işleyiş, ilkeler, roller. Metinler alınmış kararlara dayanır; iddia yok. */
export function AboutPage() {
  return (
    <div className="overview about">
      <Hero
        greeting="AI-Guard nedir?"
        text="Çevrim içi sınavlar için sıfır güven ilkesiyle çalışan, açıklanabilir bir kopya tespit platformu. Sinyalleri bir risk skoruna çevirir, gerekçesini gösterir; kararı insana bırakır."
      />

      <section className="overview-card" aria-labelledby="how-title">
        <h2 className="overview-card-title" id="how-title">
          Nasıl çalışır?
        </h2>
        <div className="pipeline">
          <ol className="pipeline-sources">
            {SOURCES.map((source, index) => (
              <motion.li key={source.title} initial={{ opacity: 0, x: -10 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.1 + index * 0.06 }}>
                <IconTile icon={source.icon} tone={source.tone} size="sm" />
                <span>
                  <strong>{source.title}</strong>
                  <span>{source.text}</span>
                </span>
              </motion.li>
            ))}
          </ol>
          <ArrowRight className="pipeline-arrow" size={28} aria-hidden="true" />
          <div className="pipeline-step">
            <IconTile icon={ScrollText} tone="indigo" size="lg" />
            <strong>Açıklanabilir risk skoru</strong>
            <span>Her olayın türü, süresi ve tekrarı skora ne kadar katkı yaptığıyla birlikte kaydedilir.</span>
          </div>
          <ArrowRight className="pipeline-arrow" size={28} aria-hidden="true" />
          <div className="pipeline-step">
            <IconTile icon={Gavel} tone="mint" size="lg" />
            <strong>İnsan kararı</strong>
            <span>Eşiği aşan oturum değerlendiriciye düşer; o, olayları ve kanıtı inceleyip karar verir.</span>
          </div>
        </div>
      </section>

      <section className="overview-card" aria-labelledby="principles-title">
        <h2 className="overview-card-title" id="principles-title">
          İlkeler
        </h2>
        <ul className="feature-grid">
          {PRINCIPLES.map((item) => (
            <li key={item.title}>
              <IconTile icon={item.icon} tone={item.tone} />
              <strong>{item.title}</strong>
              <span>{item.text}</span>
            </li>
          ))}
        </ul>
      </section>

      <section className="overview-card" aria-labelledby="roles-title">
        <h2 className="overview-card-title" id="roles-title">
          Kim ne yapar?
        </h2>
        <ul className="feature-grid feature-grid-3">
          {ROLES.map((item) => (
            <li key={item.title}>
              <IconTile icon={item.icon} tone={item.tone} />
              <strong>{item.title}</strong>
              <span>{item.text}</span>
            </li>
          ))}
        </ul>
      </section>

      <section className="overview-card about-project">
        <IconTile icon={GraduationCap} tone="indigo" size="lg" />
        <div>
          <h2 className="overview-card-title">Araştırma projesi</h2>
          <p>AI-Guard, Doğuş Üniversitesi YM 491 Araştırma Projesi kapsamında geliştirilmektedir.</p>
        </div>
      </section>
    </div>
  )
}
