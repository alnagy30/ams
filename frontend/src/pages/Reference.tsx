import { useState } from 'react'
import { api } from '../api'
import { Confirm, Field, Loading, Modal, Tabs, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { useLookups } from '../lookups'
import type { Accessory, Supplier, TreeNode } from '../types'

type TreeKind = 'categories' | 'departments' | 'locations'

const KIND_LABEL: Record<TreeKind, string> = {
  categories: 'التصنيفات',
  departments: 'الإدارات',
  locations: 'المواقع'
}

export default function Reference() {
  const [tab, setTab] = useState('departments')
  const lookups = useLookups()

  if (lookups.error) return <div className="card"><div className="alert error">{lookups.error}</div></div>
  if (!lookups.data) return <Loading />

  const treeTabs = (['departments', 'categories', 'locations'] as TreeKind[]).map((k) => ({ key: k, label: KIND_LABEL[k] }))
  const flatTabs = [
    { key: 'suppliers', label: 'الموردون' },
    { key: 'accessories', label: 'الملحقات' }
  ]
  const tabs = [...treeTabs, ...flatTabs]

  return (
    <div>
      <div className="page-head">
        <h1>البيانات المرجعية</h1>
      </div>
      <div className="card">
        <Tabs tabs={tabs} value={tab} onChange={setTab} />
        {tab === 'departments' && <TreeSection kind="departments" roots={lookups.data.departments} reload={lookups.reload} />}
        {tab === 'categories' && <TreeSection kind="categories" roots={lookups.data.categories} reload={lookups.reload} />}
        {tab === 'locations' && <TreeSection kind="locations" roots={lookups.data.locations} reload={lookups.reload} />}
        {tab === 'suppliers' && <FlatSection kind="suppliers" items={lookups.data.suppliers} reload={lookups.reload} />}
        {tab === 'accessories' && <FlatSection kind="accessories" items={lookups.data.accessories} reload={lookups.reload} />}
      </div>
    </div>
  )
}

/* ----------------------------- Tree ----------------------------- */

interface FlatRow {
  id: number
  name: string
  parentId: number | null
  depth: number
  hasChildren: boolean
}

function flatten(nodes: TreeNode[]): FlatRow[] {
  const out: FlatRow[] = []
  const walk = (list: TreeNode[], depth: number) => {
    for (const n of list) {
      out.push({ id: n.id, name: n.name, parentId: n.parentId, depth, hasChildren: (n.children?.length ?? 0) > 0 })
      if (n.children?.length) walk(n.children, depth + 1)
    }
  }
  walk(nodes, 0)
  return out
}

function TreeSection({ kind, roots, reload }: { kind: TreeKind; roots: TreeNode[]; reload: () => Promise<void> }) {
  const { canWrite } = useAuth()
  const toast = useToast()
  const rows = flatten(roots)
  const [modal, setModal] = useState<{ parentId: number | null } | null>(null)
  const [rename, setRename] = useState<FlatRow | null>(null)
  const [del, setDel] = useState<FlatRow | null>(null)

  const base = '/api/reference/' + kind

  return (
    <div>
      <div className="toolbar">
        {canWrite && (
          <button className="btn btn-primary btn-sm" onClick={() => setModal({ parentId: null })}>
            + عنصر رئيسي جديد
          </button>
        )}
      </div>
      {rows.length === 0 ? (
        <div className="muted center">لا توجد عناصر بعد</div>
      ) : (
        <div className="tbl-wrap">
          <table className="tbl">
            <thead>
              <tr>
                <th>الاسم</th>
                <th style={{ width: 60 }}>أفرع</th>
                <th style={{ width: 260 }}></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id}>
                  <td style={{ paddingInlineStart: 12 + r.depth * 22 }}>{r.name}</td>
                  <td className="center">{r.hasChildren ? rows.filter((x) => x.parentId === r.id).length : '—'}</td>
                  <td>
                    {canWrite && (
                      <div className="row-actions">
                        <button className="btn btn-sm" onClick={() => setModal({ parentId: r.id })}>إضافة فرع</button>
                        <button className="btn btn-ghost btn-sm" onClick={() => setRename(r)}>إعادة تسمية</button>
                        <button className="btn btn-danger btn-sm" onClick={() => setDel(r)}>حذف</button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modal && (
        <NodeModal
          title={'إضافة ' + (modal.parentId === null ? 'عنصر رئيسي' : 'عنصر فرعي')}
          onClose={() => setModal(null)}
          onSave={async (name) => {
            try {
              await api.post(base, { name, parentId: modal.parentId })
              toast('تمت الإضافة')
              setModal(null)
              await reload()
            } catch (e) {
              toast(errText(e), 'error')
            }
          }}
        />
      )}
      {rename && (
        <NodeModal
          title="إعادة تسمية"
          initial={rename.name}
          onClose={() => setRename(null)}
          onSave={async (name) => {
            try {
              await api.put(base + '/' + rename.id, { name, parentId: rename.parentId })
              toast('تمت إعادة التسمية')
              setRename(null)
              await reload()
            } catch (e) {
              toast(errText(e), 'error')
            }
          }}
        />
      )}
      <Confirm
        open={!!del}
        title={'حذف من ' + KIND_LABEL[kind]}
        message={'حذف «' + (del?.name ?? '') + '»؟ ' + (del?.hasChildren ? 'لن يُحذف إن احتوى أفرعًا.' : '')}
        danger
        onCancel={() => setDel(null)}
        onConfirm={async () => {
          try {
            await api.del(base + '/' + del!.id)
            toast('تم الحذف')
            await reload()
          } catch (e) {
            toast(errText(e), 'error')
          }
          setDel(null)
        }}
      />
    </div>
  )
}

function NodeModal({ title, initial = '', onClose, onSave }: { title: string; initial?: string; onClose: () => void; onSave: (name: string) => Promise<void> }) {
  const [name, setName] = useState(initial)
  const [busy, setBusy] = useState(false)
  return (
    <Modal
      open
      onClose={onClose}
      title={title}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button
            className="btn btn-primary"
            disabled={busy || !name.trim()}
            onClick={async () => {
              setBusy(true)
              await onSave(name.trim())
              setBusy(false)
            }}
          >
            حفظ
          </button>
        </>
      }
    >
      <Field label="الاسم" required>
        <input className="input" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </Field>
    </Modal>
  )
}

/* ----------------------------- Flat (suppliers/accessories) ----------------------------- */

function FlatSection({ kind, items, reload }: { kind: 'suppliers' | 'accessories'; items: (Supplier | Accessory)[]; reload: () => Promise<void> }) {
  const { canWrite } = useAuth()
  const toast = useToast()
  const base = '/api/reference/' + kind
  const [editor, setEditor] = useState<{ open: boolean; item?: Supplier | Accessory }>({ open: false })
  const [del, setDel] = useState<(Supplier | Accessory) | null>(null)

  return (
    <div>
      <div className="toolbar">
        {canWrite && (
          <button className="btn btn-primary btn-sm" onClick={() => setEditor({ open: true })}>
            + إضافة جديد
          </button>
        )}
      </div>
      <div className="tbl-wrap">
        <table className="tbl">
          <thead>
            <tr>
              <th>الاسم</th>
              {kind === 'suppliers' && <th>بيانات التواصل</th>}
              {kind === 'accessories' && <th>نوع</th>}
              <th style={{ width: 140 }}></th>
            </tr>
          </thead>
          <tbody>
            {items.map((it) => (
              <tr key={it.id}>
                <td>{it.name}</td>
                {kind === 'suppliers' && <td className="muted">{(it as Supplier).contactInfo ?? '—'}</td>}
                {kind === 'accessories' && <td>{'isLibraryItem' in it && it.isLibraryItem ? 'من المكتبة' : 'خاص'}</td>}
                <td>
                  {canWrite && (
                    <div className="row-actions">
                      <button className="btn btn-ghost btn-sm" onClick={() => setEditor({ open: true, item: it })}>تعديل</button>
                      <button className="btn btn-danger btn-sm" onClick={() => setDel(it)}>حذف</button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editor.open && (
        <FlatModal
          kind={kind}
          item={editor.item}
          onClose={() => setEditor({ open: false })}
          onSaved={async () => {
            setEditor({ open: false })
            toast('تم الحفظ')
            await reload()
          }}
        />
      )}
      <Confirm
        open={!!del}
        title="حذف"
        message={'حذف «' + (del?.name ?? '') + '»؟'}
        danger
        onCancel={() => setDel(null)}
        onConfirm={async () => {
          try {
            await api.del(base + '/' + del!.id)
            toast('تم الحذف')
            await reload()
          } catch (e) {
            toast(errText(e), 'error')
          }
          setDel(null)
        }}
      />
    </div>
  )
}

function FlatModal({ kind, item, onClose, onSaved }: { kind: 'suppliers' | 'accessories'; item?: Supplier | Accessory; onClose: () => void; onSaved: () => Promise<void> }) {
  const base = '/api/reference/' + kind
  const editing = !!item
  const [name, setName] = useState(item?.name ?? '')
  const [contact, setContact] = useState(kind === 'suppliers' ? ((item as Supplier)?.contactInfo ?? '') : '')
  const [isLib, setIsLib] = useState(kind === 'accessories' ? ((item as Accessory)?.isLibraryItem ?? false) : false)
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setErr('')
    if (!name.trim()) return setErr('الاسم مطلوب')
    setBusy(true)
    try {
      const body = kind === 'suppliers' ? { name: name.trim(), contactInfo: contact || null } : { name: name.trim(), isLibraryItem: isLib }
      if (editing) await api.put(base + '/' + item!.id, body)
      else await api.post(base, body)
      await onSaved()
    } catch (e) {
      setErr(errText(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={(editing ? 'تعديل ' : 'إضافة ') + (kind === 'suppliers' ? 'مورد' : 'ملحق')}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button className="btn btn-primary" onClick={save} disabled={busy}>حفظ</button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <Field label="الاسم" required>
        <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      {kind === 'suppliers' && (
        <Field label="بيانات التواصل">
          <input className="input" value={contact} onChange={(e) => setContact(e.target.value)} />
        </Field>
      )}
      {kind === 'accessories' && (
        <label className="checkbox-row mt">
          <input type="checkbox" checked={isLib} onChange={(e) => setIsLib(e.target.checked)} />
          ملحق قياسي (من مكتبة الملحقات العامة)
        </label>
      )}
    </Modal>
  )
}
