import {
  BookOpenCheck,
  Camera,
  ClipboardCheck,
  ClipboardList,
  FileBarChart,
  Info,
  LayoutDashboard,
  ScrollText,
  Settings2,
  UserRound,
  Users,
  type LucideIcon,
} from 'lucide-react'
import type { TileTone } from '../components/IconTile'

/** Her bölümün simgesi ve rengi; menüde, sayfa başlığında ve genel bakışta aynı (tanınırlık). */
export const SECTION_ICON: Record<string, { icon: LucideIcon; tone: TileTone }> = {
  '/overview': { icon: LayoutDashboard, tone: 'indigo' },
  '/admin/exams': { icon: BookOpenCheck, tone: 'sky' },
  '/admin/users': { icon: Users, tone: 'lilac' },
  '/admin/reports': { icon: FileBarChart, tone: 'mint' },
  '/admin/settings': { icon: Settings2, tone: 'amber' },
  '/admin/audit': { icon: ScrollText, tone: 'rose' },
  '/review': { icon: ClipboardCheck, tone: 'sky' },
  '/exams': { icon: ClipboardList, tone: 'sky' },
  '/profile/photo': { icon: Camera, tone: 'rose' },
  '/about': { icon: Info, tone: 'lilac' },
  '/profile': { icon: UserRound, tone: 'slate' },
}
