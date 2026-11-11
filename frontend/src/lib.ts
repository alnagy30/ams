import { formatMoney } from './currency'
import type { TreeNode } from './types'

export const STATUS_LABELS: Record<string, string> = {
  Active: 'نشط',
  UnderMaintenance: 'تحت الصيانة',
  InStorage: 'في المخزن',
  Scrapped: 'مُخرد',
  Missing: 'مفقود',
  Suspended: 'موقوف',
  Planned: 'مجدول',
  InProgress: 'قيد التنفيذ',
  Completed: 'مكتمل',
  Cancelled: 'ملغي',
  Pending: 'قيد المراجعة',
  Approved: 'معتمد',
  Rejected: 'مرفوض',
  Open: 'مفتوحة',
  Resolved: 'تمت المعالجة',
  Individual: 'عهدة فردية',
  DepartmentGeneral: 'عهدة إدارة',
  Image: 'صورة',
  Invoice: 'فاتورة',
  Document: 'مستند',
  VehicleLicenseExpiry: 'انتهاء رخصة مركبة',
  InventoryDeadline: 'موعد جرد',
  FailedLogin: 'محاولة دخول فاشلة',
  MissingData: 'بيانات ناقصة',
  InventoryMismatch: 'اختلاف بالجرد',
  UserLockedOut: 'قفل حساب',
  Generic: 'عام'
}

export const ROLE_LABELS: Record<string, string> = {
  Admin: 'مدير النظام',
  AssetManager: 'أمين الأصول',
  Viewer: 'مطالعة'
}

export const PERMISSION_LABELS: Record<string, string> = {
  Dashboard: 'لوحة المعلومات',
  ManageUsers: 'إدارة المستخدمين',
  ManageEmployees: 'إدارة الموظفين',
  ReferenceData: 'البيانات المرجعية',
  Notifications: 'التنبيهات',
  Backup: 'النسخ الاحتياطي',
  Import: 'استيراد البيانات',
  BulkTransfer: 'نقل جماعي',
  FinancialValue: 'القيمة المالية',
  AssetEditLog: 'سجل تعديل الأصول',
  TransferHistory: 'سجل الحركات',
  InventoryHistory: 'سجل الجرد',
  ScrapHistory: 'سجل التخريد',
  AuditLogs: 'سجلات التدقيق',
  OrgStructureHistory: 'سجل الهيكل التنظيمي'
}

const LRI = '\u2066'
const PDI = '\u2069'

function two(n: number): string {
  return String(n).padStart(2, '0')
}

/** Day / Month / Year as dd/MM/yyyy (bidi-isolated so it never renders reversed). */
export function fmtDate(value?: string | null): string {
  if (!value) return '—'
  const d = new Date(value)
  if (isNaN(d.getTime())) return value
  return LRI + two(d.getDate()) + '/' + two(d.getMonth() + 1) + '/' + d.getFullYear() + PDI
}

/** Day / Month / Year + HH:mm (24h), bidi-isolated. */
export function fmtDateTime(value?: string | null): string {
  if (!value) return '—'
  const d = new Date(value)
  if (isNaN(d.getTime())) return value
  return (
    LRI +
    two(d.getDate()) + '/' + two(d.getMonth() + 1) + '/' + d.getFullYear() +
    ' ' + two(d.getHours()) + ':' + two(d.getMinutes()) +
    PDI
  )
}

export function fmtMoney(value?: number | null): string {
  return formatMoney(value)
}

export function fmtBytes(value?: number | null): string {
  if (!value) return '—'
  if (value < 1024) return value + ' B'
  if (value < 1024 * 1024) return (value / 1024).toFixed(1) + ' KB'
  return (value / (1024 * 1024)).toFixed(2) + ' MB'
}

export function label(key: string | undefined | null): string {
  if (!key) return '—'
  return STATUS_LABELS[key] ?? key
}

export function todayISO(): string {
  return new Date().toISOString().slice(0, 10)
}

/** Returns all nodes flattened with depth/path for <option> rendering */
export interface FlatNode {
  id: number
  name: string
  parentId: number | null
  depth: number
  path: string
  hasChildren: boolean
}

export function flattenTree(nodes: TreeNode[], depth = 0, parentPath = ''): FlatNode[] {
  const out: FlatNode[] = []
  for (const n of nodes) {
    const path = parentPath ? parentPath + ' / ' + n.name : n.name
    out.push({
      id: n.id,
      name: n.name,
      parentId: n.parentId,
      depth,
      path,
      hasChildren: (n.children?.length ?? 0) > 0
    })
    if (n.children?.length) out.push(...flattenTree(n.children, depth + 1, path))
  }
  return out
}

/** Only leaf items (valid targets for assets/custody) */
export function leaves(nodes: TreeNode[]): FlatNode[] {
  return flattenTree(nodes).filter((n) => !n.hasChildren)
}

export function pad(n: number, size = 3): string {
  return String(n).padStart(size, '0')
}
