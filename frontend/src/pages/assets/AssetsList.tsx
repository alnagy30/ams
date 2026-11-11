import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../../api'
import { Empty, Loading, Pagination } from '../../components/ui'
import { useAuth } from '../../auth'
import { useLookups, useTree } from '../../lookups'
import { useCurrency, formatMoney } from '../../currency'
import { label } from '../../lib'
import type { AssetListItem } from '../../types'

const PAGE_SIZE = 20

export default function AssetsList() {
  const { user, canWrite } = useAuth()
  const canSeeFin = user?.role === 'Admin' || (user?.permissions ?? []).includes('FinancialValue')
  const nav = useNavigate()
  const lookups = useLookups()
  const currency = useCurrency()

  const [items, setItems] = useState<AssetListItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)

  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [custody, setCustody] = useState('')

  const cats = useTree(lookups.data?.categories)
  const depts = useTree(lookups.data?.departments)

  const query = useMemo(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      search: search || undefined,
      status: status || undefined,
      categoryId: categoryId || undefined,
      departmentId: departmentId || undefined,
      custodyType: custody || undefined
    }),
    [page, search, status, categoryId, departmentId, custody]
  )

  useEffect(() => {
    setPage(1)
  }, [search, status, categoryId, departmentId, custody])

  useEffect(() => {
    let live = true
    setLoading(true)
    api
      .get<{ items: AssetListItem[]; totalCount: number }>('/api/assets', query)
      .then((d) => {
        if (!live) return
        setItems(d.items)
        setTotal(d.totalCount)
      })
      .finally(() => live && setLoading(false))
    return () => {
      live = false
    }
  }, [query, page])

  return (
    <div>
      <div className="page-head">
        <h1>سجل الأصول</h1>
        {canWrite && (
          <button className="btn btn-primary" onClick={() => nav('/assets/new')}>
            + أصل جديد
          </button>
        )}
      </div>

      <div className="card">
        <div className="toolbar">
          <input className="input w-40" placeholder="بحث بالاسم أو الكود أو السيريال..." value={search} onChange={(e) => setSearch(e.target.value)} />
          <select className="input" style={{ maxWidth: 180 }} value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
            <option value="">كل التصنيفات</option>
            {cats.map((c) => (
              <option key={c.value} value={c.value}>
                {'\u00A0\u00A0'.repeat(c.depth) + c.label}
              </option>
            ))}
          </select>
          <select className="input" style={{ maxWidth: 180 }} value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
            <option value="">كل الإدارات</option>
            {depts.map((c) => (
              <option key={c.value} value={c.value}>
                {'\u00A0\u00A0'.repeat(c.depth) + c.label}
              </option>
            ))}
          </select>
          <select className="input" style={{ maxWidth: 160 }} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">كل الحالات</option>
            {['Active', 'UnderMaintenance', 'InStorage', 'Missing', 'Suspended'].map((s) => (
              <option key={s} value={s}>
                {label(s)}
              </option>
            ))}
          </select>
          <select className="input" style={{ maxWidth: 160 }} value={custody} onChange={(e) => setCustody(e.target.value)}>
            <option value="">كل العهد</option>
            <option value="Individual">فردية</option>
            <option value="DepartmentGeneral">عهدة إدارة</option>
          </select>
        </div>

        {loading ? (
          <Loading />
        ) : items.length === 0 ? (
          <Empty text="لا توجد أصول مطابقة" />
        ) : (
          <>
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>الكود</th>
                    <th>الاسم</th>
                    <th>التصنيف</th>
                    <th>السيريال</th>
                    <th>الحالة</th>
                    <th>العهدة</th>
                    <th>الإدارة / الموقع</th>
                    {canSeeFin && <th>القيمة</th>}
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((a) => (
                    <tr key={a.id}>
                      <td><Link to={'/assets/' + a.id}>{a.assetCode}</Link></td>
                      <td>
                        {a.name}
                        {a.isVehicle && <span className="badge blue" style={{ marginInlineStart: 6 }}>مركبة</span>}
                      </td>
                      <td>
                        {a.categoryName}
                        {a.subCategoryName ? <div className="muted">{a.subCategoryName}</div> : null}
                      </td>
                      <td className="muted">{a.serialNumber ?? '—'}</td>
                      <td>
                        <span className={'badge ' + (a.status === 'Active' ? 'green' : a.status === 'UnderMaintenance' || a.status === 'Missing' ? 'amber' : a.status === 'Scrapped' ? 'grey' : 'red')}>
                          {label(a.status)}
                        </span>
                      </td>
                      <td>{a.custodianName ?? '—'}</td>
                      <td className="muted">
                        {a.departmentName}
                        <div>{a.locationName}</div>
                      </td>
                      {canSeeFin && <td>{a.purchaseValue != null ? formatMoney(a.purchaseValue, currency) : '—'}</td>}
                      <td>
                        <div className="row-actions">
                          <button className="btn btn-ghost btn-sm" onClick={() => nav('/assets/' + a.id)}>
                            عرض
                          </button>
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
    </div>
  )
}
