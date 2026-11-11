import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'

/* ----------------------------- Toasts ----------------------------- */

interface ToastMsg {
  id: number
  text: string
  kind: 'success' | 'error' | 'info'
}

const ToastCtx = createContext<(text: string, kind?: ToastMsg['kind']) => void>(() => {})

export function useToast() {
  return useContext(ToastCtx)
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastMsg[]>([])
  const idRef = useRef(0)

  const push = useCallback((text: string, kind: ToastMsg['kind'] = 'success') => {
    const id = ++idRef.current
    setToasts((t) => [...t, { id, text, kind }])
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 4200)
  }, [])

  const value = useMemo(() => push, [push])

  return (
    <ToastCtx.Provider value={value}>
      {children}
      <div className="toast-wrap">
        {toasts.map((t) => (
          <div key={t.id} className={'toast ' + t.kind} role="status" aria-live="polite">
            <span className="ico">{t.kind === 'error' ? <XIcon /> : t.kind === 'info' ? <InfoIcon /> : <CheckIcon />}</span>
            <span>{t.text}</span>
          </div>
        ))}
      </div>
    </ToastCtx.Provider>
  )
}

/* ----------------------------- helpers ----------------------------- */

export function errText(e: unknown, fallback = 'حدث خطأ'): string {
  if (e instanceof Error) return e.message || fallback
  return fallback
}

/* ----------------------------- Modal ----------------------------- */

export function Modal({
  open,
  onClose,
  title,
  children,
  footer,
  wide
}: {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  footer?: ReactNode
  wide?: boolean
}) {
  if (!open) return null
  return (
    <div className="overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className={'modal' + (wide ? ' wide' : '')}>
        <div className="modal-head">
          <h3>{title}</h3>
          <button className="btn btn-ghost btn-sm" onClick={onClose} aria-label="إغلاق">
            <XIcon />
          </button>
        </div>
        <div>{children}</div>
        {footer && <div className="modal-actions">{footer}</div>}
      </div>
    </div>
  )
}

export function Confirm({
  open,
  title,
  message,
  confirmText = 'تأكيد',
  danger,
  busy,
  onConfirm,
  onCancel
}: {
  open: boolean
  title: string
  message: string
  confirmText?: string
  danger?: boolean
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <Modal
      open={open}
      onClose={onCancel}
      title={title}
      footer={
        <>
          <button className="btn" onClick={onCancel} disabled={busy}>
            إلغاء
          </button>
          <button className={'btn ' + (danger ? 'btn-danger' : 'btn-primary')} onClick={onConfirm} disabled={busy}>
            {busy ? '...' : confirmText}
          </button>
        </>
      }
    >
      <p style={{ margin: 0 }}>{message}</p>
    </Modal>
  )
}

/* ----------------------------- Field ----------------------------- */

export function Field({
  label,
  required,
  children,
  hint,
  error
}: {
  label: string
  required?: boolean
  children: ReactNode
  hint?: string
  error?: string
}) {
  return (
    <div className="field">
      <label className="field-label">
        {label}
        {required && <span style={{ color: 'var(--danger)' }}> *</span>}
      </label>
      {children}
      {error ? (
        <div className="field-error">{error}</div>
      ) : (
        hint && <div className="muted" style={{ marginTop: 3 }}>{hint}</div>
      )}
    </div>
  )
}

/* ----------------------------- DateInput ----------------------------- */

function isoToDisplay(iso?: string): string {
  if (!iso) return ''
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso)
  if (!m) return ''
  return `${m[3]}/${m[2]}/${m[1]}`
}

function displayToIso(text: string): string | null {
  const s = text.trim()
  if (!s) return ''
  const m = /^(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{2,4})$/.exec(s)
  if (!m) return null
  const d = Number(m[1])
  let mo = Number(m[2])
  let y = Number(m[3])
  if (y < 100) y += 2000
  if (mo > 12 && d <= 12) {
    const t = mo
    mo = d
    return buildIso(y, mo, t)
  }
  return buildIso(y, mo, d)
}

function buildIso(y: number, mo: number, d: number): string | null {
  if (mo < 1 || mo > 12 || d < 1 || d > 31) return null
  const iso = `${y}-${String(mo).padStart(2, '0')}-${String(d).padStart(2, '0')}`
  const dt = new Date(iso + 'T00:00:00')
  if (dt.getFullYear() !== y || dt.getMonth() !== mo - 1 || dt.getDate() !== d) return null
  return iso
}

export function DateInput({
  value,
  onChange,
  min,
  max,
  disabled,
  className,
  autoFocus,
  placeholder = 'يوم/شهر/سنة'
}: {
  value: string
  onChange: (iso: string) => void
  min?: string
  max?: string
  disabled?: boolean
  className?: string
  autoFocus?: boolean
  placeholder?: string
}) {
  const nativeRef = useRef<HTMLInputElement>(null)
  const [text, setText] = useState(() => isoToDisplay(value))

  useEffect(() => {
    setText(isoToDisplay(value))
  }, [value])

  const openPicker = () => {
    const el = nativeRef.current
    if (!el) return
    if (typeof el.showPicker === 'function') {
      try {
        el.showPicker()
        return
      } catch {
        /* fall through */
      }
    }
    el.focus()
    el.click()
  }

  return (
    <div className="date-input">
      <input
        className={className ?? 'input'}
        dir="ltr"
        inputMode="numeric"
        autoComplete="off"
        placeholder={placeholder}
        value={text}
        disabled={disabled}
        autoFocus={autoFocus}
        onChange={(e) => {
          const t = e.target.value
          setText(t)
          const iso = displayToIso(t)
          if (iso !== null) onChange(iso)
        }}
        onBlur={() => setText(isoToDisplay(value))}
      />
      <button type="button" className="date-pick" disabled={disabled} onClick={openPicker} tabIndex={-1} aria-label="اختيار من التقويم">
        <CalendarIcon />
      </button>
      <input
        ref={nativeRef}
        type="date"
        className="date-native"
        value={value || ''}
        min={min}
        max={max}
        disabled={disabled}
        tabIndex={-1}
        aria-hidden="true"
        onChange={(e) => {
          onChange(e.target.value)
          setText(isoToDisplay(e.target.value))
        }}
      />
    </div>
  )
}

function CalendarIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="4" width="18" height="18" rx="2" />
      <path d="M16 2v4M8 2v4M3 10h18" />
    </svg>
  )
}

/* ----------------------------- Pagination ----------------------------- */

export function Pagination({
  page,
  pageSize,
  total,
  onPage
}: {
  page: number
  pageSize: number
  total: number
  onPage: (p: number) => void
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(total, page * pageSize)
  return (
    <div className="pagination">
      <span className="muted">
        عرض {from}–{to} من {total}
      </span>
      <div className="flex">
        <button className="btn btn-sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          السابق
        </button>
        <span>
          صفحة {page} من {pages}
        </span>
        <button className="btn btn-sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>
          التالي
        </button>
      </div>
    </div>
  )
}

/* ----------------------------- Misc ----------------------------- */

export function Loading({ text = 'جارٍ التحميل...' }: { text?: string }) {
  return (
    <div className="empty">
      <div className="flex" style={{ justifyContent: 'center' }}>
        <span className="spinner lg" /> <span>{text}</span>
      </div>
    </div>
  )
}

export function Empty({ text = 'لا توجد بيانات' }: { text?: string }) {
  return (
    <div className="empty">
      <div className="big">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
          <path d="M21 8 12 3 3 8v8l9 5 9-5z" />
          <path d="m3 8 9 5 9-5M12 13v8" />
        </svg>
      </div>
      {text}
    </div>
  )
}

export function Tabs({
  tabs,
  value,
  onChange
}: {
  tabs: { key: string; label: string }[]
  value: string
  onChange: (k: string) => void
}) {
  return (
    <div className="tabs">
      {tabs.map((t) => (
        <button key={t.key} className={'tab' + (t.key === value ? ' active' : '')} onClick={() => onChange(t.key)}>
          {t.label}
        </button>
      ))}
    </div>
  )
}

/* ----------------------------- Icons ----------------------------- */

const ICON_PROPS = {
  width: 16,
  height: 16,
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2.2,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const
}

function XIcon() {
  return <svg {...ICON_PROPS}><path d="M18 6 6 18M6 6l12 12" /></svg>
}

function CheckIcon() {
  return <svg {...ICON_PROPS}><path d="M20 6 9 17l-5-5" /></svg>
}

function InfoIcon() {
  return <svg {...ICON_PROPS}><circle cx="12" cy="12" r="9" /><path d="M12 16v-5M12 8h.01" /></svg>
}

/* ----------------------------- Tree option helpers ----------------------------- */

export function treeToOptions(
  nodes: { id: number; name: string; children: { id: number; name: string; children?: unknown[] }[] }[],
  onlyLeaf = false
): { value: string; label: string; depth: number }[] {
  const out: { value: string; label: string; depth: number }[] = []
  const walk = (list: typeof nodes, depth: number) => {
    for (const n of list) {
      const kids = (n.children ?? []) as { id: number; name: string; children?: unknown[] }[]
      if (!onlyLeaf || kids.length === 0) out.push({ value: String(n.id), label: n.name, depth })
      if (kids.length) walk(kids as typeof nodes, depth + 1)
    }
  }
  walk(nodes, 0)
  return out
}
