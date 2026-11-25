import type { Role } from '../auth/roles'

export interface OnboardingStep {
  title: string
  text: string
  /** lucide ikon adı; Lottie animasyonları gelince bu alanın yerine geçecek. */
  icon: 'list' | 'camera' | 'shield' | 'clock' | 'folder' | 'chart' | 'gavel' | 'users' | 'settings' | 'book'
}

/**
 * İlk girişte rol bazlı tanıtım (PRODUCT.md, yön bulma). Metinler platformun gerçek davranışını anlatır; her biri bir
 * karara dayanır (K-24, K-27, K-30, K-31, K-32). Yeni ekran gelince ilgili rolün adımları güncellenir.
 */
export const ONBOARDING: Record<Role, OnboardingStep[]> = {
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
}
