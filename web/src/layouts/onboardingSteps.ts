import type { Role } from '../auth/roles'
import { defineMessages, live } from '../i18n/messages'

export interface OnboardingStep {
  title: string
  text: string
  /** lucide ikon adı; Lottie animasyonları gelince bu alanın yerine geçecek. */
  icon: 'list' | 'camera' | 'shield' | 'clock' | 'folder' | 'chart' | 'gavel' | 'users' | 'settings' | 'book' | 'spark' | 'scale' | 'info'
}

/**
 * İlk girişte rol bazlı tanıtım (PRODUCT.md, yön bulma). Metinler platformun gerçek davranışını anlatır; her biri bir
 * karara dayanır (K-24, K-27, K-30, K-31, K-32). Yeni ekran gelince ilgili rolün adımları iki dilde güncellenir.
 * Her rolün tanıtımı platformun ne olduğuyla başlar: kullanıcı sitenin amacını ilk ekranda öğrensin.
 */
function build(intro: OnboardingStep[], outro: OnboardingStep, steps: Record<Role, OnboardingStep[]>): Record<Role, OnboardingStep[]> {
  return {
    Candidate: [...intro, ...steps.Candidate, outro],
    Reviewer: [...intro, ...steps.Reviewer, outro],
    Admin: [...intro, ...steps.Admin, outro],
  }
}

const tr = build(
  [
    {
      icon: 'spark',
      title: 'AI-Guard nedir?',
      text: 'Çevrim içi sınavlar için açıklanabilir bir kopya tespit platformu. Kamera, mikrofon, tarayıcı, cihaz ve ağdan gelen sinyalleri bir risk skoruna çevirir ve her skorun gerekçesini gösterir.',
    },
    {
      icon: 'scale',
      title: 'Kararı insan verir',
      text: 'Sistem kimseyi suçlamaz. Riskli bulunan oturumu yetkili bir değerlendirici inceler; olaylar kopya kanıtı sayılmaz, kararı değerlendirici verir. Sürekli kayıt tutulmaz, yalnızca şüpheli anların kanıtı saklanır.',
    },
  ],
  {
    icon: 'info',
    title: 'Takıldığınız yerde',
    text: 'Sayfa başlığındaki "i" düğmesi o sayfada ne yapılacağını kısaca söyler. Bu tanıtımı yan menüdeki "Tanıtımı yeniden göster" ile istediğiniz zaman açabilirsiniz.',
  },
  {
    Candidate: [
      {
        icon: 'list',
        title: 'Sınavlarınız tek yerde',
        text: '"Sınavlarım" sayfasında size atanan sınavları, erişim saatlerini ve her sınavın durumunu görürsünüz. Sınav açılınca "Başlat" düğmesi belirir.',
      },
      {
        icon: 'camera',
        title: 'Önce kayıt fotoğrafı',
        text: 'Sınavdan önce bir kez kayıt fotoğrafı yüklersiniz. Sınav başında kimliğiniz bu fotoğrafla doğrulanır; açık rızanız olmadan biyometrik işlem yapılmaz.',
      },
      {
        icon: 'shield',
        title: 'Sınav öncesi dört adım',
        text: 'Aydınlatma metnini onaylar, güvenlik ajanını eşleştirir, kuralları okur ve kimliğinizi doğrularsınız. Süre ancak kimliğiniz doğrulanınca başlar.',
      },
      {
        icon: 'clock',
        title: 'Sınav sırasında',
        text: 'Kamera ve mikrofon açık kalır, cevaplarınız her seçimde kaydedilir. Bağlantınız koparsa cevaplar cihazda bekler. Kararı sistem değil, değerlendirici verir.',
      },
    ],
    Reviewer: [
      {
        icon: 'list',
        title: 'Atandığınız sınavlar',
        text: '"İncelemeler" sayfasında değerlendirici olduğunuz sınavları ve her birinde kararınızı bekleyen dosya sayısını görürsünüz.',
      },
      {
        icon: 'folder',
        title: 'Öncelikli dosyalar önde',
        text: 'İnceleme panelinde riskli ve işaretli oturumlar en üstte. Yeni bir dosya düştüğünde sayfa kendiliğinden güncellenir.',
      },
      {
        icon: 'chart',
        title: 'Skorun gerekçesi açık',
        text: 'Dosyada olaylar zaman çizgisinde, skora katkıları tek tek görünür. Yanlış alarm işaretlediğiniz olay skordan çıkar ama silinmez.',
      },
      {
        icon: 'gavel',
        title: 'Kararı siz verirsiniz',
        text: 'Temiz, Şüpheli ya da İhlal: karar bir kez verilir ve değiştirilemez. Notlarınız ve kararınız aday raporunda yer alır.',
      },
    ],
    Admin: [
      {
        icon: 'book',
        title: 'Sınavları kurun',
        text: 'Sınav oluşturur, sorularını yazar, adayları ve değerlendiricileri atarsınız. Erişim penceresi ve süre sınav başına ayarlanır.',
      },
      {
        icon: 'users',
        title: 'Kullanıcılar ve davetler',
        text: 'Hesapları siz açarsınız; kullanıcıya şifre belirleme bağlantısı gider. Rolleri değiştirebilir, hesapları pasife alabilirsiniz.',
      },
      {
        icon: 'chart',
        title: 'Raporlar',
        text: 'Her adayın sınav sonucu, risk skoru, olayları ve değerlendirici kararı tek raporda; PDF ya da Excel olarak indirilebilir.',
      },
      {
        icon: 'settings',
        title: 'Kayıt ve gizlilik',
        text: 'Kim neyi ne zaman yaptı, denetim kaydında; kayıt değiştirilemez. Saklama süresi dolan kanıtlar kendiliğinden silinir.',
      },
    ],
  },
)

const en = build(
  [
    {
      icon: 'spark',
      title: 'What is AI-Guard?',
      text: 'An explainable cheating-detection platform for online exams. It turns signals from the camera, microphone, browser, device and network into a risk score and shows the reason behind every score.',
    },
    {
      icon: 'scale',
      title: 'A person makes the decision',
      text: 'The system accuses no one. A qualified reviewer examines any session found risky; events are not proof of cheating and the reviewer decides. Nothing is recorded continuously; only evidence of suspicious moments is kept.',
    },
  ],
  {
    icon: 'info',
    title: 'If you get stuck',
    text: 'The "i" button next to each page title briefly explains what to do on that page. You can open this tour again at any time with "Show the tour again" in the side menu.',
  },
  {
    Candidate: [
      {
        icon: 'list',
        title: 'All your exams in one place',
        text: 'The "My exams" page shows the exams assigned to you, their access times and the status of each one. When an exam opens, a "Start" button appears.',
      },
      {
        icon: 'camera',
        title: 'Reference photo first',
        text: 'Before your first exam you upload a reference photo once. At the start of each exam your identity is checked against it; no biometric processing happens without your explicit consent.',
      },
      {
        icon: 'shield',
        title: 'Four steps before the exam',
        text: 'You accept the privacy notice, pair the security agent, read the rules and verify your identity. The timer only starts once your identity is verified.',
      },
      {
        icon: 'clock',
        title: 'During the exam',
        text: 'Your camera and microphone stay on and every answer is saved as you choose it. If your connection drops, answers wait on your device. A reviewer, not the system, makes the decision.',
      },
    ],
    Reviewer: [
      {
        icon: 'list',
        title: 'Your assigned exams',
        text: 'The "Reviews" page lists the exams you review and how many files in each are waiting for your decision.',
      },
      {
        icon: 'folder',
        title: 'Priority files first',
        text: 'Risky and flagged sessions are at the top of the review panel. When a new file arrives, the page updates by itself.',
      },
      {
        icon: 'chart',
        title: 'Every score is explained',
        text: 'Each file shows events on a timeline with their contribution to the score. An event you mark as a false alarm leaves the score but is never deleted.',
      },
      {
        icon: 'gavel',
        title: 'You make the decision',
        text: 'Clean, Suspicious or Violation: a decision is made once and cannot be changed. Your notes and decision appear in the candidate report.',
      },
    ],
    Admin: [
      {
        icon: 'book',
        title: 'Set up exams',
        text: 'You create exams, write their questions and assign candidates and reviewers. The access window and duration are set per exam.',
      },
      {
        icon: 'users',
        title: 'Users and invitations',
        text: 'You open accounts; each user receives a link to set their password. You can change roles and deactivate accounts.',
      },
      {
        icon: 'chart',
        title: 'Reports',
        text: 'Each candidate’s result, risk score, events and reviewer decision are in one report, downloadable as PDF or Excel.',
      },
      {
        icon: 'settings',
        title: 'Records and privacy',
        text: 'Who did what and when is kept in the audit log, which cannot be changed. Evidence whose retention period has ended is deleted automatically.',
      },
    ],
  },
)

export const ONBOARDING: Record<Role, OnboardingStep[]> = live(defineMessages<Record<Role, OnboardingStep[]>>({ tr, en }))
