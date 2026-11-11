import { useCallback, useEffect, useState } from 'react'
import { api } from '../api'
import { Confirm, DateInput, Empty, Field, Loading, Modal, Pagination, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { useLookups } from '../lookups'
import { fmtDate } from '../lib'
import type { Employee } from '../types'

const PAGE_SIZE = 20

export default function Employees() {
  const { canWrite } = useAuth()
  const lookups = useLookups()
  const toast = useToast()

  const [items, setItems] = useState<Employee[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [dept, setDept] = useState('')
  const [includeDeleted, setIncludeDeleted] = useState(false)
  const [loading, setLoading] = useState(true)

  const [editor, setEditor] = useState<{ open: boolean; item?: Employee }>({ open: false })
  const [leaveFor, setLeaveFor] = useState<Employee | null>(null)
  const [reactivateFor, setReactivateFor] = useState<Employee | null>(null)

  const reload = useCallback(() => {
    setLoading(true)
    api
      .get<{ items: Employee[]; totalCount: number }>('/api/employees', {
        search: search || undefined,
        departmentId: dept || undefined,
        includeDeleted: includeDeleted || undefined,
        page,
        pageSize: PAGE_SIZE
      })
      .then((d) => {
        setItems(d.items)
        setTotal(d.totalCount)
      })
      .catch((e) => toast(errText(e), 'error'))
      .finally(() => setLoading(false))
  }, [page, search, dept, includeDeleted])

  useEffect(() => {
    reload()
  }, [reload])

  useEffect(() => {
    setPage(1)
  }, [search, dept, includeDeleted])

  const deptOpts = lookups.data?.departments ?? []
  const deptFlat: { value: string; label: string; depth: number }[] = []
  const walkDept = (list: typeof deptOpts, d: number) => {
    for (const n of list) {
      deptFlat.push({ value: String(n.id), label: n.name, depth: d })
      if (n.children?.length) walkDept(n.children, d + 1)
    }
  }
  walkDept(deptOpts, 0)

  return (
    <div>
      <div className="page-head">
        <h1>الموظفون</h1>
        {canWrite && (
          <button className="btn btn-primary" onClick={() => setEditor({ open: true })}>
            + موظف جديد
          </button>
        )}
      </div>

      <div className="card">
        <div className="toolbar">
          <input className="input w-40" placeholder="بحث بالاسم أو الرقم الوطني..." value={search} onChange={(e) => setSearch(e.target.value)} />
          <select className="input" style={{ maxWidth: 200 }} value={dept} onChange={(e) => setDept(e.target.value)}>
            <option value="">كل الإدارات</option>
            {deptFlat.map((d) => (
              <option key={d.value} value={d.value}>
                {'\u00A0\u00A0'.repeat(d.depth) + d.label}
              </option>
            ))}
          </select>
          <label className="checkbox-row">
            <input type="checkbox" checked={includeDeleted} onChange={(e) => setIncludeDeleted(e.target.checked)} />
            إظهار المفصولين
          </label>
        </div>

        {loading ? (
          <Loading />
        ) : items.length === 0 ? (
          <Empty />
        ) : (
          <>
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>الاسم</th>
                    <th>الرقم الوطني</th>
                    <th>المسمى الوظيفي</th>
                    <th>الإدارة</th>
                    <th>أصول بعهدته</th>
                    <th>تاريخ الإضافة</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((em) => (
                    <tr key={em.id}>
                      <td>
                        {em.fullName}
                        {em.isDeleted && <span className="badge red" style={{ marginInlineStart: 6 }}>مفصول</span>}
                      </td>
                      <td className="muted">{em.nationalId}</td>
                      <td>{em.jobTitle ?? '—'}</td>
                      <td>{em.departmentName}</td>
                      <td>{em.heldAssetsCount}</td>
                      <td className="muted">{fmtDate(em.createdAt)}</td>
                      <td>
                        <div className="row-actions">
                          {canWrite && !em.isDeleted && (
                            <>
                              <button className="btn btn-ghost btn-sm" onClick={() => setEditor({ open: true, item: em })}>
                                تعديل
                              </button>
                              <button className="btn btn-sm" onClick={() => setLeaveFor(em)}>
                                إنهاء خدمة
                              </button>
                            </>
                          )}
                          {canWrite && em.isDeleted && (
                            <button className="btn btn-sm" onClick={() => setReactivateFor(em)}>
                              إعادة تفعيل
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} pageSize={PAGE_SIZE} total={total} onPage={setPage} />
          </>
        )}
      </div>

      {editor.open && (
        <EmployeeModal
          item={editor.item}
          onClose={() => setEditor({ open: false })}
          onSaved={() => {
            setEditor({ open: false })
            reload()
          }}
        />
      )}
      {leaveFor && (
        <LeaveModal
          employee={leaveFor}
          onClose={() => setLeaveFor(null)}
          onDone={() => {
            setLeaveFor(null)
            reload()
          }}
        />
      )}
      <Confirm
        open={!!reactivateFor}
        title="إعادة تفعيل موظف"
        message={'إعادة تفعيل الموظف ' + (reactivateFor?.fullName ?? '') + '؟'}
        onCancel={() => setReactivateFor(null)}
        onConfirm={async () => {
          try {
            await api.post('/api/employees/' + reactivateFor!.id + '/reactivate', {})
            toast('تمت إعادة التفعيل')
            reload()
          } catch (e) {
            toast(errText(e), 'error')
          }
          setReactivateFor(null)
        }}
      />
    </div>
  )
}

function EmployeeModal({ item, onClose, onSaved }: { item?: Employee; onClose: () => void; onSaved: () => void }) {
  const lookups = useLookups()
  const toast = useToast()
  const editing = !!item
  const [fullName, setFullName] = useState(item?.fullName ?? '')
  const [nationalId, setNationalId] = useState(item?.nationalId ?? '')
  const [jobTitle, setJobTitle] = useState(item?.jobTitle ?? '')
  const [departmentId, setDepartmentId] = useState(item ? String(item.departmentId) : '')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  const save = async () => {
    setErr('')
    if (!fullName.trim() || !nationalId.trim() || !departmentId) return setErr('الاسم والرقم الوطني والإدارة مطلوبة.')
    setBusy(true)
    try {
      if (editing) {
        await api.put('/api/employees/' + item!.id, { fullName, nationalId, jobTitle: jobTitle || null, departmentId: Number(departmentId) })
      } else {
        await api.post('/api/employees', { fullName, nationalId, jobTitle: jobTitle || null, departmentId: Number(departmentId) })
      }
      toast(editing ? 'تم تحديث الموظف' : 'تمت إضافة الموظف')
      onSaved()
    } catch (e) {
      setErr(errText(e))
      setBusy(false)
    }
  }

  const flat = lookups.data?.departments ?? []
  const options: { value: string; label: string; depth: number }[] = []
  const walk = (list: typeof flat, d: number) => {
    for (const n of list) {
      options.push({ value: String(n.id), label: n.name, depth: d })
      if (n.children?.length) walk(n.children, d + 1)
    }
  }
  walk(flat, 0)

  return (
    <Modal
      open
      onClose={onClose}
      title={editing ? 'تعديل موظف' : 'إضافة موظف'}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button className="btn btn-primary" onClick={save} disabled={busy}>{busy ? '...' : 'حفظ'}</button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <Field label="الاسم الكامل" required>
        <input className="input" value={fullName} onChange={(e) => setFullName(e.target.value)} />
      </Field>
      <Field label="الرقم الوطني" required>
        <input className="input" value={nationalId} onChange={(e) => setNationalId(e.target.value)} />
      </Field>
      <Field label="المسمى الوظيفي">
        <input className="input" value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} />
      </Field>
      <Field label="الإدارة" required>
        <select className="input" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
          <option value="">— اختر —</option>
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {'\u00A0\u00A0'.repeat(o.depth) + o.label}
            </option>
          ))}
        </select>
      </Field>
    </Modal>
  )
}

function LeaveModal({ employee, onClose, onDone }: { employee: Employee; onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [replacement, setReplacement] = useState('')
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10))
  const [others, setOthers] = useState<Employee[]>([])
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  useEffect(() => {
    api.get<{ items: Employee[] }>('/api/employees', { page: 1, pageSize: 500 }).then((d) => setOthers(d.items.filter((x) => x.id !== employee.id)))
  }, [employee.id])

  return (
    <Modal
      open
      onClose={onClose}
      title={'إنهاء خدمة: ' + employee.fullName}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button
            className="btn btn-danger"
            disabled={busy}
            onClick={async () => {
              setErr('')
              setBusy(true)
              try {
                await api.post('/api/employees/' + employee.id + '/leave', {
                  replacementEmployeeId: replacement ? Number(replacement) : null,
                  effectiveDate: date
                })
                toast('تم إنهاء الخدمة ونقل العهد إن وُجدت')
                onDone()
              } catch (e) {
                setErr(errText(e))
                setBusy(false)
              }
            }}
          >
            {busy ? '...' : 'إنهاء الخدمة'}
          </button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <div className="muted mb">تُنقل الأصول المعهودة إلى الموظف البديل تلقائيًا إن حُدد.</div>
      <Field label="الموظف البديل لنقل العهد (اختياري)">
        <select className="input" value={replacement} onChange={(e) => setReplacement(e.target.value)}>
          <option value="">— بدون نقل —</option>
          {others.map((o) => (
            <option key={o.id} value={o.id}>
              {o.fullName} — {o.departmentName}
            </option>
          ))}
        </select>
      </Field>
      <Field label="تاريخ السريان" required>
        <DateInput value={date} onChange={setDate} />
      </Field>
    </Modal>
  )
}
