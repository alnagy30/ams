import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, Field, Loading, Modal, Pagination, Tabs, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { useLookups } from '../lookups'
import { fmtDate, label } from '../lib'
import type { AssetListItem, CustodyRecord, Employee } from '../types'

const PAGE_SIZE = 20

export default function Custody() {
  const [tab, setTab] = useState('ledger')
  return (
    <div>
      <div className="page-head">
        <h1>العهد والمقتنيات</h1>
      </div>
      <div className="card">
        <Tabs
          tabs={[
            { key: 'ledger', label: 'سجل العهد' },
            { key: 'unassigned', label: 'أصول بلا عهدة' }
          ]}
          value={tab}
          onChange={setTab}
        />
        {tab === 'ledger' ? <Ledger /> : <Unassigned />}
      </div>
    </div>
  )
}

function Ledger() {
  const lookups = useLookups()
  const [items, setItems] = useState<CustodyRecord[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [openOnly, setOpenOnly] = useState(true)
  const [dept, setDept] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)

  const flatDepts = lookups.data?.departments ?? []
  const deptOpts: { value: string; label: string; depth: number }[] = []
  const walk = (list: typeof flatDepts, d: number) => {
    for (const n of list) {
      deptOpts.push({ value: String(n.id), label: n.name, depth: d })
      if (n.children?.length) walk(n.children, d + 1)
    }
  }
  walk(flatDepts, 0)

  useEffect(() => {
    setPage(1)
  }, [search, dept, openOnly])

  useEffect(() => {
    let live = true
    setLoading(true)
    api
      .get<{ items: CustodyRecord[]; totalCount: number }>('/api/assets/custody', {
        openOnly: openOnly || undefined,
        departmentId: dept || undefined,
        page,
        pageSize: PAGE_SIZE
      })
      .then((d) => live && setItems(d.items) && setTotal(d.totalCount))
      .catch((e) => live && console.error(e))
      .finally(() => live && setLoading(false))
    return () => {
      live = false
    }
  }, [page, search, dept, openOnly])

  return (
    <div>
      <div className="toolbar">
        <label className="checkbox-row">
          <input type="checkbox" checked={openOnly} onChange={(e) => setOpenOnly(e.target.checked)} />
          العهد المفتوحة فقط
        </label>
        <select className="input" style={{ maxWidth: 200 }} value={dept} onChange={(e) => setDept(e.target.value)}>
          <option value="">كل الإدارات</option>
          {deptOpts.map((d) => (
            <option key={d.value} value={d.value}>
              {'\u00A0\u00A0'.repeat(d.depth) + d.label}
            </option>
          ))}
        </select>
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
                  <th>الأصل</th>
                  <th>نوع العهدة</th>
                  <th>الحائز</th>
                  <th>تاريخ التسليم</th>
                  <th>تاريخ الإرجاع</th>
                  <th>الحالة</th>
                </tr>
              </thead>
              <tbody>
                {items.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <Link to={'/assets/' + c.assetId}>
                        {c.assetCode} — {c.assetName}
                      </Link>
                    </td>
                    <td>{label(c.custodyType)}</td>
                    <td>{c.custodianName ?? 'عهدة عامة'}</td>
                    <td>{fmtDate(c.handoverDate)}</td>
                    <td>{c.returnDate ? fmtDate(c.returnDate) : '—'}</td>
                    <td>
                      <span className={'badge ' + (c.isOpen ? 'green' : 'grey')}>{c.isOpen ? 'مفتوحة' : 'مغلقة'}</span>
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
  )
}

function Unassigned() {
  const { canWrite } = useAuth()
  const toast = useToast()
  const lookups = useLookups()
  const [assets, setAssets] = useState<AssetListItem[]>([])
  const [employees, setEmployees] = useState<Employee[]>([])
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [dept, setDept] = useState('')
  const [assignEmp, setAssignEmp] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.get<{ items: Employee[] }>('/api/employees', { page: 1, pageSize: 500 }).then((d) => setEmployees(d.items))
  }, [])

  useEffect(() => {
    let live = true
    setLoading(true)
    api
      .get<AssetListItem[]>('/api/assets/unassigned', { departmentId: dept || undefined })
      .then((d) => {
        if (!live) return
        setAssets(d)
        setSelected(new Set())
      })
      .catch(() => {})
      .finally(() => live && setLoading(false))
    return () => {
      live = false
    }
  }, [dept])

  const toggle = (id: number) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const assign = async () => {
    if (!assignEmp || selected.size === 0) return
    setBusy(true)
    try {
      const n = await api.post<number>('/api/assets/custody/assign-many', {
        employeeId: Number(assignEmp),
        assetIds: Array.from(selected)
      })
      toast('تم منح العهدة لـ ' + n + ' أصل')
      setSelected(new Set())
      setAssets(assets.filter((a) => !selected.has(a.id)))
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div>
      <div className="toolbar">
        <select className="input" style={{ maxWidth: 220 }} value={dept} onChange={(e) => setDept(e.target.value)}>
          <option value="">كل الإدارات</option>
          {lookups.data?.departments.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
        <div className="grow" />
        {canWrite && (
          <>
            <select className="input" style={{ maxWidth: 260 }} value={assignEmp} onChange={(e) => setAssignEmp(e.target.value)}>
              <option value="">— الموظف الحائز —</option>
              {employees.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName} — {e.departmentName}
                </option>
              ))}
            </select>
            <button className="btn btn-primary" disabled={selected.size === 0 || !assignEmp || busy} onClick={assign}>
              منح عهدة لـ{selected.size > 0 ? ' ' + selected.size + ' أصل' : ''}
            </button>
          </>
        )}
      </div>

      {loading ? (
        <Loading />
      ) : assets.length === 0 ? (
        <Empty text="كل الأصول مُعهدة حاليًا" />
      ) : (
        <div className="tbl-wrap">
          <table className="tbl">
            <thead>
              <tr>
                <th style={{ width: 40 }}>
                  {canWrite && (
                    <input
                      type="checkbox"
                      checked={selected.size === assets.length}
                      onChange={(e) => setSelected(e.target.checked ? new Set(assets.map((a) => a.id)) : new Set())}
                    />
                  )}
                </th>
                <th>الكود</th>
                <th>الاسم</th>
                <th>الإدارة</th>
                <th>الموقع</th>
              </tr>
            </thead>
            <tbody>
              {assets.map((a) => (
                <tr key={a.id}>
                  <td>
                    {canWrite && <input type="checkbox" checked={selected.has(a.id)} onChange={() => toggle(a.id)} />}
                  </td>
                  <td>{a.assetCode}</td>
                  <td>
                    <Link to={'/assets/' + a.id}>{a.name}</Link>
                  </td>
                  <td>{a.departmentName}</td>
                  <td className="muted">{a.locationName}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
