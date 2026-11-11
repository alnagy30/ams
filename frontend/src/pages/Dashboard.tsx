import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, Loading, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { fmtDate, fmtMoney, label } from '../lib'

interface NameCount {
  name: string
  count: number
}

interface MonthCount {
  month: string
  label: string
  count: number
}

interface ExpiringLicense {
  assetId: number
  assetCode: string
  name: string
  plateNumber?: string | null
  licenseExpiryDate: string
  daysRemaining: number
}

interface Dashboard {
  totalActiveAssets: number
  totalEmployees: number
  totalDepartments: number
  openNotifications: number
  inProgressCampaigns: number
  expiringVehicleLicenses: number
  missingDataAssets: number
  totalPurchaseValue: number
  statusDistribution: { status: string; label: string; count: number }[]
  assetsByMonth: MonthCount[]
  topCategories: NameCount[]
  topDepartments: NameCount[]
  expiringLicenses: ExpiringLicense[]
  recentlyAdded: {
    id: number
    assetCode: string
    name: string
    categoryName: string
    subCategoryName?: string | null
    status: string
    custodianName?: string | null
    departmentName: string
    locationName: string
    purchaseValue?: number | null
  }[]
  recentActivities: {
    id: number
    userName: string
    action: string
    entityType: string
    entityId?: number | null
    timestamp: string
  }[]
}

const STATUS_COLORS: Record<string, string> = {
  Active: 'var(--success)',
  UnderMaintenance: 'var(--warn)',
  InStorage: 'var(--primary)',
  Suspended: 'var(--accent)',
  Missing: 'var(--danger)'
}

export default function Dashboard() {
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState('')
  const toast = useToast()
  const { canWrite, hasPerm } = useAuth()

  useEffect(() => {
    api
      .get<Dashboard>('/api/dashboard')
      .then(setData)
      .catch((e) => {
        setError(errText(e))
        toast(errText(e), 'error')
      })
  }, [])

  if (error) return <div className="card"><div className="alert error">{error}</div></div>
  if (!data) return <Loading />

  return (
    <div>
      <div className="page-head">
        <h1>لوحة المعلومات</h1>
        <div className="dash-actions">
          {canWrite && (
            <Link to="/assets/new" className="btn btn-primary btn-sm">
              + إضافة أصل
            </Link>
          )}
          {hasPerm('Import') && (
            <Link to="/import" className="btn btn-sm">
              استيراد Excel
            </Link>
          )}
          {hasPerm('InventoryHistory') && (
            <Link to="/inventory" className="btn btn-sm">
              جرد جديد
            </Link>
          )}
        </div>
      </div>

      <div className="kpis">
        <div className="kpi">
          <div className="n">{data.totalActiveAssets}</div>
          <div className="l">أصل نشط</div>
        </div>
        <div className="kpi good">
          <div className="n money">{fmtMoney(data.totalPurchaseValue)}</div>
          <div className="l">إجمالي قيمة الأصول</div>
        </div>
        <div className="kpi">
          <div className="n">{data.totalEmployees}</div>
          <div className="l">موظف</div>
        </div>
        <div className="kpi">
          <div className="n">{data.totalDepartments}</div>
          <div className="l">إدارة</div>
        </div>
        <div className="kpi warn">
          <div className="n">{data.openNotifications}</div>
          <div className="l">تنبيه مفتوح</div>
        </div>
        <div className="kpi">
          <div className="n">{data.inProgressCampaigns}</div>
          <div className="l">جرد قيد التنفيذ</div>
        </div>
        <div className="kpi danger">
          <div className="n">{data.expiringVehicleLicenses}</div>
          <div className="l">رخصة مركبة قاربت على الانتهاء</div>
        </div>
        <div className="kpi warn">
          <div className="n">{data.missingDataAssets}</div>
          <div className="l">أصل ببيانات ناقصة</div>
        </div>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <div className="section-title">توزيع الأصول حسب الحالة</div>
          {data.statusDistribution.length === 0 ? (
            <Empty text="لا توجد أصول" />
          ) : (
            <StatusDonut distribution={data.statusDistribution} />
          )}
        </div>

        <div className="card">
          <div className="section-title">الأصول المضافة خلال 6 أشهر</div>
          <MonthColumns data={data.assetsByMonth} />
        </div>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <div className="section-title">أكثر التصنيفات</div>
          <RankBars data={data.topCategories} empty="لا توجد تصنيفات" />
        </div>
        <div className="card">
          <div className="section-title">أكثر الإدارات</div>
          <RankBars data={data.topDepartments} empty="لا توجد إدارات" />
        </div>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <div className="flex-between">
            <div className="section-title" style={{ margin: 0 }}>رخص مركبات قاربت على الانتهاء</div>
            <span className="muted">خلال 30 يومًا</span>
          </div>
          {data.expiringLicenses.length === 0 ? (
            <div className="empty-inline">لا توجد رخص قاربة على الانتهاء.</div>
          ) : (
            data.expiringLicenses.map((l) => (
              <div key={l.assetId} className="license-row">
                <Link to={'/assets/' + l.assetId} className="badge blue">{l.assetCode}</Link>
                <div>
                  <div style={{ fontWeight: 600, fontSize: '0.85rem' }}>{l.name}</div>
                  <div className="muted" style={{ fontSize: '0.75rem' }}>
                    {l.plateNumber ?? '—'} · تنتهي {fmtDate(l.licenseExpiryDate)}
                  </div>
                </div>
                <span className={'badge ' + (l.daysRemaining <= 7 ? 'red' : 'amber') + ' license-days'}>
                  {l.daysRemaining < 0 ? 'منتهية' : l.daysRemaining + ' يوم'}
                </span>
              </div>
            ))
          )}
        </div>

        <div className="card">
          <div className="section-title">أحدث الأنشطة</div>
          {data.recentActivities.length === 0 && <Empty />}
          {data.recentActivities.slice(0, 8).map((a) => (
            <div key={a.id} className="flex-between" style={{ padding: '0.3rem 0', borderBottom: '1px dashed var(--border)' }}>
              <div>
                <strong>{a.userName}</strong> — {label(a.action)} <span className="muted">{a.entityType}</span>
              </div>
              <div className="muted">{fmtDate(a.timestamp)}</div>
            </div>
          ))}
        </div>
      </div>

      <div className="card">
        <div className="flex-between">
          <div className="section-title" style={{ margin: 0 }}>أحدث الأصول المضافة</div>
          <Link to="/assets" className="btn btn-ghost btn-sm">عرض الكل</Link>
        </div>
        {data.recentlyAdded.length === 0 && <Empty text="لا توجد أصول بعد" />}
        <div className="tbl-wrap">
          <table className="tbl">
            <thead>
              <tr>
                <th>الكود</th>
                <th>الاسم</th>
                <th>التصنيف</th>
                <th>الحالة</th>
                <th>العهدة</th>
                <th>الإدارة / الموقع</th>
                <th>القيمة</th>
              </tr>
            </thead>
            <tbody>
              {data.recentlyAdded.map((a) => (
                <tr key={a.id}>
                  <td><Link to={'/assets/' + a.id}>{a.assetCode}</Link></td>
                  <td>{a.name}</td>
                  <td>{a.categoryName}{a.subCategoryName ? ' / ' + a.subCategoryName : ''}</td>
                  <td><span className="badge blue">{label(a.status)}</span></td>
                  <td>{a.custodianName ?? '—'}</td>
                  <td className="muted">{a.departmentName} · {a.locationName}</td>
                  <td className="muted">{a.purchaseValue != null ? fmtMoney(a.purchaseValue) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

function StatusDonut({ distribution }: { distribution: { status: string; label: string; count: number }[] }) {
  const total = distribution.reduce((s, d) => s + d.count, 0)
  const r = 54
  const c = 2 * Math.PI * r
  let acc = 0
  return (
    <div className="donut-wrap">
      <div className="donut">
        <svg width="160" height="160" viewBox="0 0 160 160">
          <circle cx="80" cy="80" r={r} fill="none" stroke="var(--surface-3)" strokeWidth="20" />
          {distribution.map((d) => {
            const len = total === 0 ? 0 : (d.count / total) * c
            const el = (
              <circle
                key={d.status}
                cx="80"
                cy="80"
                r={r}
                fill="none"
                stroke={STATUS_COLORS[d.status] ?? 'var(--muted)'}
                strokeWidth="20"
                strokeDasharray={`${len} ${c - len}`}
                strokeDashoffset={-acc}
              />
            )
            acc += len
            return el
          })}
        </svg>
        <div className="center">
          <div className="v">{total}</div>
          <div className="t">إجمالي الأصول</div>
        </div>
      </div>
      <div className="legend">
        {distribution.map((d) => (
          <div key={d.status} className="legend-row">
            <span className="legend-dot" style={{ background: STATUS_COLORS[d.status] ?? 'var(--muted)' }} />
            <span>{label(d.label)}</span>
            <span className="v">{d.count}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function MonthColumns({ data }: { data: MonthCount[] }) {
  const max = Math.max(1, ...data.map((d) => d.count))
  return (
    <div className="col-chart">
      {data.map((d) => (
        <div key={d.month} className="col-item">
          <div className="col-val">{d.count}</div>
          <div className="col-bar" style={{ height: `${Math.max(3, (d.count / max) * 100)}%` }} />
          <div className="col-label">{d.label}</div>
        </div>
      ))}
    </div>
  )
}

function RankBars({ data, empty }: { data: NameCount[]; empty: string }) {
  if (data.length === 0) return <div className="empty-inline">{empty}</div>
  const max = Math.max(1, ...data.map((d) => d.count))
  return (
    <div className="chart-grid">
      {data.map((d) => (
        <div key={d.name} className="bar-row">
          <span style={{ minWidth: 120, fontSize: '0.82rem' }}>{d.name}</span>
          <div className="bar-track">
            <div className="bar-fill" style={{ width: `${(d.count / max) * 100}%` }} />
          </div>
          <span style={{ minWidth: 24, fontWeight: 700 }}>{d.count}</span>
        </div>
      ))}
    </div>
  )
}
