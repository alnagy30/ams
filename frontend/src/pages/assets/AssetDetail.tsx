import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, download, triggerDownload } from '../../api'
import { Confirm, DateInput, Field, Loading, Modal, errText } from '../../components/ui'
import { useToast } from '../../components/ui'
import { useAuth } from '../../auth'
import { useLookups } from '../../lookups'
import { useCurrency, formatMoney } from '../../currency'
import { fmtBytes, fmtDate, label } from '../../lib'
import type { AssetAccessory, AssetDetail, Employee } from '../../types'

export default function AssetDetailPage() {
  const { id } = useParams()
  const nav = useNavigate()
  const toast = useToast()
  const { user, canWrite } = useAuth()
  const canSeeFin = user?.role === 'Admin' || (user?.permissions ?? []).includes('FinancialValue')
  const lookups = useLookups()
  const currency = useCurrency()

  const [asset, setAsset] = useState<AssetDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [assignOpen, setAssignOpen] = useState(false)
  const [delOpen, setDelOpen] = useState(false)
  const [accOpen, setAccOpen] = useState(false)
  const [busy, setBusy] = useState(false)

  const reload = useCallback(async () => {
    try {
      setError('')
      setAsset(await api.get<AssetDetail>('/api/assets/' + id))
    } catch (e) {
      setError(errText(e))
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => {
    setLoading(true)
    reload()
  }, [reload])

  const accessoryMap = useMemo(() => {
    const m = new Map<number, string>()
    for (const a of lookups.data?.accessories ?? []) m.set(a.id, a.name)
    return m
  }, [lookups.data])

  if (loading) return <Loading />
  if (error || !asset)
    return (
      <div className="card">
        <div className="alert error">{error || 'الأصل غير موجود'}</div>
        <button className="btn" onClick={() => nav('/assets')}>
          عودة للسجل
        </button>
      </div>
    )

  const custOpen = !!asset.custodianId || !!asset.custodyType

  return (
    <div>
      <div className="page-head">
        <h1>
          {asset.assetCode} — {asset.name}
          {asset.isVehicle && <span className="badge blue" style={{ marginInlineStart: 8 }}>مركبة</span>}
        </h1>
        <div className="actions">
          {canWrite && (
            <>
              <button className="btn" onClick={() => nav('/assets/' + id + '/edit')}>
                تعديل
              </button>
              {!custOpen && (
                <button className="btn btn-primary" onClick={() => setAssignOpen(true)}>
                  منح عهدة
                </button>
              )}
              <button className="btn btn-danger" onClick={() => setDelOpen(true)}>
                حذف
              </button>
            </>
          )}
          <button
            className="btn"
            onClick={async () => {
              try {
                triggerDownload(await download('/api/assets/' + id + '/card'))
              } catch (e) {
                toast(errText(e), 'error')
              }
            }}
          >
            بطاقة QR (PDF)
          </button>
        </div>
      </div>

      {asset.hasMissingData && (
        <div className="alert info">بيانات ناقصة: {asset.missingDataFields.join('، ')}</div>
      )}

      <div className="grid grid-2">
        <div className="card">
          <div className="section-title">البيانات الأساسية</div>
          <div className="detail-grid">
            <div className="detail-item">
              <div className="k">كود الأصل</div>
              <div className="v">{asset.assetCode}</div>
            </div>
            <div className="detail-item">
              <div className="k">التصنيف</div>
              <div className="v">{asset.categoryName}</div>
            </div>
            <div className="detail-item">
              <div className="k">الحالة</div>
              <div className="v">
                <span className={'badge ' + (asset.status === 'Active' ? 'green' : 'amber')}>{label(asset.status)}</span>
              </div>
            </div>
            <div className="detail-item">
              <div className="k">الماركة</div>
              <div className="v">{asset.brand ?? '—'}</div>
            </div>
            <div className="detail-item">
              <div className="k">الموديل</div>
              <div className="v">{asset.model ?? '—'}</div>
            </div>
            <div className="detail-item">
              <div className="k">السيريال</div>
              <div className="v">{asset.serialNumber ?? '—'}</div>
            </div>
            <div className="detail-item">
              <div className="k">تاريخ الشراء</div>
              <div className="v">{fmtDate(asset.purchaseDate)}</div>
            </div>
            {canSeeFin && (
              <div className="detail-item">
                <div className="k">قيمة الشراء</div>
                <div className="v">{asset.purchaseValue != null ? formatMoney(asset.purchaseValue, currency) : '—'}</div>
              </div>
            )}
            <div className="detail-item">
              <div className="k">المورد</div>
              <div className="v">{asset.supplierName ?? '—'}</div>
            </div>
            <div className="detail-item">
              <div className="k">الإدارة</div>
              <div className="v">{asset.departmentName}</div>
            </div>
            <div className="detail-item">
              <div className="k">الموقع</div>
              <div className="v">{asset.locationName}</div>
            </div>
            {asset.notes && (
              <div className="detail-item" style={{ gridColumn: '1/-1' }}>
                <div className="k">ملاحظات</div>
                <div className="v">{asset.notes}</div>
              </div>
            )}
          </div>
        </div>

        <div className="card">
          <div className="section-title">العهدة الحالية</div>
          {!custOpen ? (
            <div className="empty">
              <div className="big">—</div>
              الأصل غير مُعهد به حاليًا (في المخزن)
            </div>
          ) : (
            <div className="detail-grid">
              <div className="detail-item">
                <div className="k">نوع العهدة</div>
                <div className="v">{label(asset.custodyType)}</div>
              </div>
              <div className="detail-item">
                <div className="k">الحائز</div>
                <div className="v">{asset.custodianName ?? 'عهدة عامة للإدارة'}</div>
              </div>
              <div className="detail-item" style={{ gridColumn: '1/-1' }}>
                <div className="k">إجراءات</div>
                <div className="flex" style={{ marginTop: 6 }}>
                  {canWrite && (
                    <>
                      <Link className="btn btn-sm" to={'/transfers?asset=' + asset.id}>
                        نقل الأصل
                      </Link>
                      <button
                        className="btn btn-danger btn-sm"
                        onClick={async () => {
                          try {
                            await api.post('/api/assets/custody/return', {
                              assetIds: [asset.id],
                              date: new Date().toISOString().slice(0, 10),
                              notes: 'إعادة إلى المخزن'
                            })
                            toast('تمت إعادة الأصل إلى المخزن')
                            reload()
                          } catch (e) {
                            toast(errText(e), 'error')
                          }
                        }}
                      >
                        إرجاع للمخزن
                      </button>
                    </>
                  )}
                </div>
              </div>
            </div>
          )}

          {asset.isVehicle && asset.vehicle && (
            <div style={{ marginTop: 12 }}>
              <div className="section-title">بيانات المركبة</div>
              <div className="detail-grid">
                <div className="detail-item">
                  <div className="k">رقم اللوحة</div>
                  <div className="v">{asset.vehicle.plateNumber ?? '—'}</div>
                </div>
                <div className="detail-item">
                  <div className="k">الشاصيه</div>
                  <div className="v">{asset.vehicle.chassisNumber ?? '—'}</div>
                </div>
                <div className="detail-item">
                  <div className="k">سنة الصنع</div>
                  <div className="v">{asset.vehicle.manufactureYear ?? '—'}</div>
                </div>
                <div className="detail-item">
                  <div className="k">اللون</div>
                  <div className="v">{asset.vehicle.color ?? '—'}</div>
                </div>
                <div className="detail-item">
                  <div className="k">انتهاء الرخصة</div>
                  <div className="v">{fmtDate(asset.vehicle.licenseExpiryDate)}</div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <div className="section-title">المرفقات</div>
          {asset.attachments.length === 0 && <div className="muted">لا توجد مرفقات</div>}
          {asset.attachments.map((att) => (
            <div key={att.id} className="qr-doc-line">
              <div>
                <span className="badge blue">{label(att.attachmentType)}</span> <span>{att.originalName}</span>
                <span className="muted"> ({fmtBytes(att.fileSize)})</span>
              </div>
              <div className="row-actions">
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={async () => {
                    try {
                      triggerDownload(await download('/api/assets/attachments/' + att.id + '/download'))
                    } catch (e) {
                      toast(errText(e), 'error')
                    }
                  }}
                >
                  تنزيل
                </button>
                {canWrite && (
                  <button
                    className="btn btn-danger btn-sm"
                    onClick={async () => {
                      try {
                        await api.del('/api/assets/attachments/' + att.id)
                        toast('تم حذف المرفق')
                        reload()
                      } catch (e) {
                        toast(errText(e), 'error')
                      }
                    }}
                  >
                    حذف
                  </button>
                )}
              </div>
            </div>
          ))}
          {canWrite && <AttachmentUpload assetId={asset.id} onDone={reload} />}
        </div>

        <div className="card">
          <div className="flex-between">
            <div className="section-title" style={{ margin: 0 }}>الملحقات</div>
            {canWrite && (
              <button className="btn btn-sm" onClick={() => setAccOpen(true)}>
                إدارة الملحقات
              </button>
            )}
          </div>
          {asset.accessories.length === 0 && <div className="muted mt">لا توجد ملحقات مسجلة</div>}
          {asset.accessories.map((ac) => (
            <div key={ac.accessoryId} className="flex-between" style={{ padding: '0.35rem 0', borderBottom: '1px dashed var(--border)' }}>
              <span>{ac.accessoryName}</span>
              <span className="badge blue">×{ac.quantity}</span>
            </div>
          ))}
        </div>
      </div>

      {assignOpen && <AssignModal assetId={asset.id} onClose={() => setAssignOpen(false)} onDone={() => { setAssignOpen(false); reload() }} />}
      {accOpen && <AccessoriesModal assetId={asset.id} initial={asset.accessories} names={accessoryMap} onClose={() => setAccOpen(false)} onDone={() => { setAccOpen(false); reload() }} />}
      <Confirm
        open={delOpen}
        title="حذف الأصل"
        message={'سيتم حذف الأصل ' + asset.assetCode + ' — هل أنت متأكد؟'}
        danger
        busy={busy}
        onCancel={() => setDelOpen(false)}
        onConfirm={async () => {
          setBusy(true)
          try {
            await api.del('/api/assets/' + asset.id)
            toast('تم حذف الأصل')
            nav('/assets')
          } catch (e) {
            toast(errText(e), 'error')
            setBusy(false)
          }
        }}
      />
    </div>
  )
}

function AttachmentUpload({ assetId, onDone }: { assetId: number; onDone: () => void }) {
  const toast = useToast()
  const [type, setType] = useState('Document')
  const [busy, setBusy] = useState(false)

  const upload = async (file: File) => {
    setBusy(true)
    try {
      const fd = new FormData()
      fd.append('type', type)
      fd.append('file', file)
      await api.post('/api/assets/' + assetId + '/attachments', { form: fd })
      toast('تم رفع المرفق')
      onDone()
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex mt">
      <select className="input" style={{ maxWidth: 130 }} value={type} onChange={(e) => setType(e.target.value)}>
        <option value="Document">مستند</option>
        <option value="Invoice">فاتورة</option>
        <option value="Image">صورة</option>
      </select>
      <label className="btn" style={{ cursor: busy ? 'not-allowed' : 'pointer' }}>
        {busy ? 'جارٍ الرفع...' : 'رفع مرفق'}
        <input
          type="file"
          className="d-none"
          disabled={busy}
          onChange={(e) => {
            const f = e.target.files?.[0]
            if (f) upload(f)
            e.target.value = ''
          }}
        />
      </label>
    </div>
  )
}

function AssignModal({ assetId, onClose, onDone }: { assetId: number; onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [type, setType] = useState('Individual')
  const [employeeId, setEmployeeId] = useState('')
  const [employees, setEmployees] = useState<Employee[]>([])
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10))
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  useEffect(() => {
    api.get<{ items: Employee[] }>('/api/employees', { page: 1, pageSize: 500, includeDeleted: false }).then((d) => setEmployees(d.items))
  }, [])

  const submit = async () => {
    setErr('')
    if (type === 'Individual' && !employeeId) return setErr('اختر الموظف الحائز للعهدة.')
    setBusy(true)
    try {
      await api.post(
        `/api/assets/${assetId}/custody/assign?employeeId=${employeeId || ''}&type=${type}&date=${date}&notes=${encodeURIComponent(notes)}`,
        {}
      )
      toast('تم منح العهدة بنجاح')
      onDone()
    } catch (e) {
      setErr(errText(e, 'تعذر منح العهدة'))
      setBusy(false)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="منح عهدة"
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button className="btn btn-primary" onClick={submit} disabled={busy}>{busy ? '...' : 'منح العهدة'}</button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <div className="checkbox-row mb">
        <label>
          <input type="radio" checked={type === 'Individual'} onChange={() => setType('Individual')} /> عهدة فردية
        </label>
        <label>
          <input type="radio" checked={type === 'DepartmentGeneral'} onChange={() => setType('DepartmentGeneral')} /> عهدة إدارية عامة
        </label>
      </div>
      <Field label={type === 'Individual' ? 'الموظف الحائز' : 'الموظف المسؤول (اختياري)'} required={type === 'Individual'}>
        <select className="input" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
          <option value="">— اختر —</option>
          {employees.map((em) => (
            <option key={em.id} value={em.id}>
              {em.fullName} — {em.departmentName}
            </option>
          ))}
        </select>
      </Field>
      <Field label="تاريخ التسليم">
        <DateInput value={date} onChange={setDate} />
      </Field>
      <Field label="ملاحظات">
        <textarea className="input" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
      </Field>
    </Modal>
  )
}

function AccessoriesModal({
  assetId,
  initial,
  names,
  onClose,
  onDone
}: {
  assetId: number
  initial: AssetAccessory[]
  names: Map<number, string>
  onClose: () => void
  onDone: () => void
}) {
  const toast = useToast()
  const { canWrite } = useAuth()
  const lookups = useLookups()
  const [rows, setRows] = useState<AssetAccessory[]>(initial)
  const [pick, setPick] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  const addRow = () => {
    const accId = Number(pick)
    if (!accId || names.get(accId) === undefined) return
    if (rows.some((r) => r.accessoryId === accId)) {
      setRows(rows.map((r) => (r.accessoryId === accId ? { ...r, quantity: r.quantity + 1 } : r)))
    } else {
      setRows([...rows, { accessoryId: accId, accessoryName: names.get(accId)!, quantity: 1, notes: null }])
    }
    setPick('')
  }

  const save = async () => {
    setErr('')
    setBusy(true)
    try {
      await api.put('/api/assets/' + assetId + '/accessories', rows.map((r) => ({ accessoryId: r.accessoryId, accessoryName: names.get(r.accessoryId) ?? r.accessoryName, quantity: r.quantity, notes: r.notes })))
      toast('تم حفظ الملحقات')
      onDone()
    } catch (e) {
      setErr(errText(e))
      setBusy(false)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="ملحقات الأصل"
      wide
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إغلاق</button>
          {canWrite && <button className="btn btn-primary" onClick={save} disabled={busy}>{busy ? '...' : 'حفظ'}</button>}
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <div className="flex">
        <select className="input" value={pick} onChange={(e) => setPick(e.target.value)}>
          <option value="">— اختر ملحقًا من القائمة —</option>
          {lookups.data?.accessories.map((a) => (
            <option key={a.id} value={a.id}>
              {a.name}
            </option>
          ))}
        </select>
        <button className="btn" onClick={addRow} disabled={!pick}>إضافة</button>
      </div>
      <table className="tbl mt">
        <thead>
          <tr>
            <th>الملحق</th>
            <th style={{ width: 90 }}>الكمية</th>
            <th style={{ width: 70 }}></th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.accessoryId}>
              <td>{names.get(r.accessoryId) ?? r.accessoryName}</td>
              <td>
                <input
                  className="input"
                  type="number"
                  min={1}
                  value={r.quantity}
                  onChange={(e) =>
                    setRows(rows.map((x) => (x.accessoryId === r.accessoryId ? { ...x, quantity: Math.max(1, Number(e.target.value)) } : x)))
                  }
                />
              </td>
              <td>
                <button className="btn btn-danger btn-sm" onClick={() => setRows(rows.filter((x) => x.accessoryId !== r.accessoryId))}>
                  حذف
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {rows.length === 0 && <div className="muted center mt">لا توجد ملحقات مضافة</div>}
    </Modal>
  )
}
