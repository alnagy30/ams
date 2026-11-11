import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, request } from '../../api'
import { Field, DateInput, errText } from '../../components/ui'
import { useToast } from '../../components/ui'
import { useAuth } from '../../auth'
import { useLookups } from '../../lookups'
import { leaves, label, todayISO } from '../../lib'
import { useCurrency } from '../../currency'
import type { AssetDetail, AssetListItem, Employee, Paged } from '../../types'

interface FormState {
  name: string
  categoryId: string
  brand: string
  model: string
  serialNumber: string
  purchaseDate: string
  purchaseValue: string
  supplierId: string
  locationId: string
  departmentId: string
  status: string
  notes: string
  inspectionNumber: string
  inspectionDate: string
  isVehicle: boolean
  plateNumber: string
  chassisNumber: string
  engineNumber: string
  manufactureYear: string
  color: string
  fuelType: string
  licenseExpiryDate: string
}

const empty: FormState = {
  name: '',
  categoryId: '',
  brand: '',
  model: '',
  serialNumber: '',
  purchaseDate: '',
  purchaseValue: '',
  supplierId: '',
  locationId: '',
  departmentId: '',
  status: 'Active',
  notes: '',
  inspectionNumber: '',
  inspectionDate: '',
  isVehicle: false,
  plateNumber: '',
  chassisNumber: '',
  engineNumber: '',
  manufactureYear: '',
  color: '',
  fuelType: '',
  licenseExpiryDate: ''
}

type Errors = Partial<Record<keyof FormState, string>>

export default function AssetForm() {
  const { id } = useParams()
  const editing = !!id
  const { canWrite } = useAuth()
  const nav = useNavigate()
  const toast = useToast()
  const lookups = useLookups()
  const currency = useCurrency()
  const [form, setForm] = useState<FormState>(empty)
  const [loading, setLoading] = useState(editing)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [errors, setErrors] = useState<Errors>({})
  const [employees, setEmployees] = useState<Employee[]>([])
  const [custodianId, setCustodianId] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [dupWarning, setDupWarning] = useState('')
  const [currentCustodian, setCurrentCustodian] = useState<string | null>(null)

  const eligibleCategories = useMemo(
    () => leaves(lookups.data?.categories ?? []).filter((c) => c.parentId != null),
    [lookups.data]
  )
  const deptOpts = useMemo(() => leaves(lookups.data?.departments ?? []), [lookups.data])
  const locOpts = useMemo(() => leaves(lookups.data?.locations ?? []), [lookups.data])
  const supplierOpts = useMemo(
    () => (lookups.data?.suppliers ?? []).filter((s) => !s.isDeleted).map((s) => ({ value: String(s.id), label: s.name })),
    [lookups.data]
  )

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) => {
    setForm((f) => ({ ...f, [k]: v }))
    setErrors((prev) => {
      if (!prev[k]) return prev
      const next = { ...prev }
      delete next[k]
      return next
    })
  }

  useEffect(() => {
    api
      .get<Paged<Employee>>('/api/employees', { pageSize: 500, includeDeleted: false })
      .then((r) => setEmployees(r.items))
      .catch(() => {})
  }, [])

  useEffect(() => {
    if (!editing) return
    api
      .get<AssetDetail>('/api/assets/' + id)
      .then((a) => {
        setForm({
          name: a.name,
          categoryId: String(a.categoryId),
          brand: a.brand ?? '',
          model: a.model ?? '',
          serialNumber: a.serialNumber ?? '',
          purchaseDate: a.purchaseDate ? a.purchaseDate.slice(0, 10) : '',
          purchaseValue: a.purchaseValue != null ? String(a.purchaseValue) : '',
          supplierId: a.supplierId != null ? String(a.supplierId) : '',
          locationId: String(a.locationId),
          departmentId: String(a.departmentId),
          status: a.status,
          notes: a.notes ?? '',
          inspectionNumber: a.inspectionNumber ?? '',
          inspectionDate: a.inspectionDate ? a.inspectionDate.slice(0, 10) : '',
          isVehicle: a.isVehicle,
          plateNumber: a.vehicle?.plateNumber ?? '',
          chassisNumber: a.vehicle?.chassisNumber ?? '',
          engineNumber: a.vehicle?.engineNumber ?? '',
          manufactureYear: a.vehicle?.manufactureYear != null ? String(a.vehicle.manufactureYear) : '',
          color: a.vehicle?.color ?? '',
          fuelType: a.vehicle?.fuelType ?? '',
          licenseExpiryDate: a.vehicle?.licenseExpiryDate ? a.vehicle.licenseExpiryDate.slice(0, 10) : ''
        })
        setCurrentCustodian(a.custodianName ?? null)
      })
      .catch((e) => setError(errText(e)))
      .finally(() => setLoading(false))
  }, [editing, id])

  const validate = (): Errors => {
    const e: Errors = {}
    if (!form.name.trim()) e.name = 'اسم الأصل مطلوب.'
    if (!form.categoryId) e.categoryId = 'اختر تصنيفًا فرعيًا.'
    if (!form.departmentId) e.departmentId = 'اختر إدارة.'
    if (!form.locationId) e.locationId = 'اختر موقعًا.'
    if (form.isVehicle && !form.plateNumber.trim()) e.plateNumber = 'رقم اللوحة مطلوب للمركبات.'
    if (form.isVehicle && !form.chassisNumber.trim()) e.chassisNumber = 'رقم الشاصيه مطلوب للمركبات.'
    if (form.purchaseValue !== '' && Number(form.purchaseValue) < 0) e.purchaseValue = 'القيمة لا يمكن أن تكون سالبة.'
    if (form.manufactureYear !== '') {
      const y = Number(form.manufactureYear)
      if (!Number.isInteger(y) || y < 1950 || y > new Date().getFullYear() + 1) e.manufactureYear = 'سنة صنع غير صحيحة.'
    }
    return e
  }

  const checkSerialDuplicate = async () => {
    const s = form.serialNumber.trim()
    if (editing || !s) return setDupWarning('')
    try {
      const r = await api.get<Paged<AssetListItem>>('/api/assets', { serialNumber: s, pageSize: 1 })
      const hit = r.items[0]
      setDupWarning(hit ? `يوجد أصل آخر بنفس الرقم التسلسلي (${hit.assetCode} — ${hit.name}).` : '')
    } catch {
      setDupWarning('')
    }
  }

  const uploadFiles = async (assetId: number) => {
    for (const f of files) {
      try {
        const fd = new FormData()
        fd.append('type', 'Document')
        fd.append('file', f)
        await request('/api/assets/' + assetId + '/attachments', { method: 'POST', form: fd })
      } catch (e) {
        toast('تعذر رفع الملف ' + f.name + ': ' + errText(e), 'error')
      }
    }
  }

  const save = async (addAnother = false) => {
    setError('')
    const e = validate()
    setErrors(e)
    if (Object.keys(e).length) {
      setError('يرجى تصحيح الحقول المعلّمة بالأحمر.')
      return
    }

    const body: Record<string, unknown> = {
      name: form.name.trim(),
      categoryId: Number(form.categoryId),
      brand: form.brand || null,
      model: form.model || null,
      serialNumber: form.serialNumber || null,
      purchaseDate: form.purchaseDate || null,
      purchaseValue: form.purchaseValue !== '' ? Number(form.purchaseValue) : null,
      supplierId: form.supplierId ? Number(form.supplierId) : null,
      locationId: Number(form.locationId),
      departmentId: Number(form.departmentId),
      status: form.status,
      notes: form.notes || null,
      inspectionNumber: form.inspectionNumber || null,
      inspectionDate: form.inspectionDate || null,
      isVehicle: form.isVehicle,
      custodianEmployeeId: !editing && custodianId ? Number(custodianId) : null
    }
    if (form.isVehicle) {
      body.vehicle = {
        plateNumber: form.plateNumber.trim(),
        chassisNumber: form.chassisNumber.trim(),
        engineNumber: form.engineNumber || null,
        manufactureYear: form.manufactureYear ? Number(form.manufactureYear) : null,
        color: form.color || null,
        fuelType: form.fuelType || null,
        licenseExpiryDate: form.licenseExpiryDate || null,
        conditionOnReceipt: 'جديد'
      }
    } else {
      body.vehicle = null
    }

    setBusy(true)
    try {
      if (editing) {
        const saved = await api.put<AssetDetail>('/api/assets/' + id, body)
        toast('تم تحديث الأصل بنجاح')
        nav('/assets/' + saved.id)
        return
      }
      const saved = await api.post<AssetDetail>('/api/assets', body)
      if (files.length) await uploadFiles(saved.id)
      toast(
        addAnother
          ? 'تم إضافة الأصل بنجاح. يمكنك إضافة أصل آخر.'
          : 'تم إضافة الأصل بنجاح' + (files.length ? ' مع المستندات.' : '.')
      )
      if (addAnother) {
        setForm({ ...empty })
        setCustodianId('')
        setFiles([])
        setDupWarning('')
        setErrors({})
      } else {
        nav('/assets/' + saved.id)
      }
    } catch (e) {
      setError(errText(e, 'تعذر الحفظ'))
    } finally {
      setBusy(false)
    }
  }

  if (!canWrite) {
    return (
      <div className="card">
        <div className="alert error">لا تملك صلاحية إضافة أو تعديل الأصول.</div>
      </div>
    )
  }
  if (loading) return <div className="card"><span className="spinner" /></div>

  const empOpts = employees.map((e) => ({ value: String(e.id), label: e.fullName + ' — ' + e.departmentName }))

  return (
    <div className="card">
      <div className="page-head">
        <h1>{editing ? 'تعديل الأصل' : 'إضافة أصل جديد'}</h1>
        <div className="actions">
          <button className="btn" onClick={() => nav(-1)}>
            رجوع
          </button>
        </div>
      </div>
      {error && <div className="alert error">{error}</div>}
      {editing && (
        <div className="alert info">
          الإدارة والموقع ونوع الأصل حقول مشتقة — لتغيير الإدارة/الموقع استخدم عملية النقل.
        </div>
      )}

      <div className="form-section">
        <div className="section-title">البيانات الأساسية</div>
        <div className="grid grid-3">
          <Field label="اسم الأصل" required error={errors.name}>
            <input className={'input' + (errors.name ? ' invalid' : '')} value={form.name} onChange={(e) => set('name', e.target.value)} />
          </Field>
          <Field
            label="التصنيف الفرعي"
            required
            error={errors.categoryId}
            hint={eligibleCategories.length === 0 ? 'أنشئ تصنيفًا فرعيًا من البيانات المرجعية أولًا.' : undefined}
          >
            <select className={'input' + (errors.categoryId ? ' invalid' : '')} value={form.categoryId} onChange={(e) => set('categoryId', e.target.value)}>
              <option value="">— اختر —</option>
              {eligibleCategories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.path}
                </option>
              ))}
            </select>
          </Field>
          <Field label="الحالة">
            <select className="input" value={form.status} onChange={(e) => set('status', e.target.value)}>
              {['Active', 'UnderMaintenance', 'InStorage', 'Missing', 'Suspended'].map((s) => (
                <option key={s} value={s}>
                  {label(s)}
                </option>
              ))}
            </select>
          </Field>
          <Field label="الماركة">
            <input className="input" value={form.brand} onChange={(e) => set('brand', e.target.value)} />
          </Field>
          <Field label="الموديل">
            <input className="input" value={form.model} onChange={(e) => set('model', e.target.value)} />
          </Field>
          <Field label="الرقم التسلسلي" hint={dupWarning || undefined}>
            <input
              className="input"
              value={form.serialNumber}
              onChange={(e) => set('serialNumber', e.target.value)}
              onBlur={checkSerialDuplicate}
            />
          </Field>
        </div>
      </div>

      <div className="form-section">
        <div className="section-title">بيانات الشراء</div>
        <div className="grid grid-3">
          <Field label="تاريخ الشراء">
            <DateInput value={form.purchaseDate} onChange={(v) => set('purchaseDate', v)} />
          </Field>
          <Field label={'قيمة الشراء (' + currency.symbol + ')'} error={errors.purchaseValue}>
            <input
              className={'input' + (errors.purchaseValue ? ' invalid' : '')}
              type="number"
              min="0"
              value={form.purchaseValue}
              onChange={(e) => set('purchaseValue', e.target.value)}
            />
          </Field>
          <Field label="المورد">
            <select className="input" value={form.supplierId} onChange={(e) => set('supplierId', e.target.value)}>
              <option value="">—</option>
              {supplierOpts.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </Field>
          <Field label="رقم الفحص / الجرد">
            <input className="input" value={form.inspectionNumber} onChange={(e) => set('inspectionNumber', e.target.value)} />
          </Field>
          <Field label="تاريخ الفحص">
            <DateInput value={form.inspectionDate} onChange={(v) => set('inspectionDate', v)} />
          </Field>
        </div>
      </div>

      <div className="form-section">
        <div className="section-title">الموقع والعهدة</div>
        <div className="grid grid-3">
          <Field label="الإدارة" required error={errors.departmentId} hint={editing ? 'مشتقة — تُعدَّل عبر النقل' : undefined}>
            <select
              className={'input' + (errors.departmentId ? ' invalid' : '')}
              value={form.departmentId}
              disabled={editing}
              onChange={(e) => set('departmentId', e.target.value)}
            >
              <option value="">— اختر —</option>
              {deptOpts.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.path}
                </option>
              ))}
            </select>
          </Field>
          <Field label="الموقع" required error={errors.locationId} hint={editing ? 'مشتق — يُعدَّل عبر النقل' : undefined}>
            <select
              className={'input' + (errors.locationId ? ' invalid' : '')}
              value={form.locationId}
              disabled={editing}
              onChange={(e) => set('locationId', e.target.value)}
            >
              <option value="">— اختر —</option>
              {locOpts.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.path}
                </option>
              ))}
            </select>
          </Field>
          {editing ? (
            <Field label="العهدة الحالية" hint="لتغيير العهدة استخدم شاشة العهد والمقتنيات">
              <input className="input" value={currentCustodian ?? 'بدون عهدة'} disabled />
            </Field>
          ) : (
            <Field label="تعيين عهدة لموظف" hint="اختياري — يُسجَّل كعهدة فردية">
              <select className="input" value={custodianId} onChange={(e) => setCustodianId(e.target.value)}>
                <option value="">بدون عهدة</option>
                {empOpts.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </Field>
          )}
        </div>
      </div>

      <div className="form-section">
        <div className="section-title">بيانات إضافية</div>
        <Field label="ملاحظات">
          <textarea className="input" rows={2} value={form.notes} onChange={(e) => set('notes', e.target.value)} />
        </Field>
        <div className="field">
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.isVehicle}
              disabled={editing}
              onChange={(e) => set('isVehicle', e.target.checked)}
            />
            أصل من نوع مركبة
          </label>
          {editing && <div className="muted" style={{ fontSize: '0.75rem' }}>لا يمكن تغيير نوع الأصل بعد الإنشاء.</div>}
        </div>
      </div>

      {form.isVehicle && (
        <div className="form-section">
          <div className="section-title">بيانات المركبة</div>
          <div className="grid grid-3">
            <Field label="رقم اللوحة" required error={errors.plateNumber}>
              <input
                className={'input' + (errors.plateNumber ? ' invalid' : '')}
                value={form.plateNumber}
                onChange={(e) => set('plateNumber', e.target.value)}
              />
            </Field>
            <Field label="رقم الشاصيه" required error={errors.chassisNumber}>
              <input
                className={'input' + (errors.chassisNumber ? ' invalid' : '')}
                value={form.chassisNumber}
                onChange={(e) => set('chassisNumber', e.target.value)}
              />
            </Field>
            <Field label="رقم الموتور">
              <input className="input" value={form.engineNumber} onChange={(e) => set('engineNumber', e.target.value)} />
            </Field>
            <Field label="سنة الصنع" error={errors.manufactureYear}>
              <input
                className={'input' + (errors.manufactureYear ? ' invalid' : '')}
                type="number"
                value={form.manufactureYear}
                onChange={(e) => set('manufactureYear', e.target.value)}
              />
            </Field>
            <Field label="اللون">
              <input className="input" value={form.color} onChange={(e) => set('color', e.target.value)} />
            </Field>
            <Field label="نوع الوقود">
              <input className="input" value={form.fuelType} onChange={(e) => set('fuelType', e.target.value)} />
            </Field>
            <Field label="تاريخ انتهاء الرخصة">
              <DateInput min={todayISO()} value={form.licenseExpiryDate} onChange={(v) => set('licenseExpiryDate', v)} />
            </Field>
          </div>
        </div>
      )}

      {!editing && (
        <div className="form-section">
          <div className="section-title">المستندات (تُرفع بعد الحفظ)</div>
          <input
            className="input"
            type="file"
            multiple
            accept="image/*,.pdf,.doc,.docx,.xls,.xlsx"
            onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
          />
          {files.length > 0 && (
            <div className="hint-inline">سيتم رفع {files.length} ملف: {files.map((f) => f.name).join('، ')}</div>
          )}
        </div>
      )}

      <div className="form-actions">
        <button className="btn" onClick={() => nav(-1)} disabled={busy}>
          إلغاء
        </button>
        {!editing && (
          <button className="btn" onClick={() => save(true)} disabled={busy}>
            حفظ وإضافة آخر
          </button>
        )}
        <button className="btn btn-primary" onClick={() => save(false)} disabled={busy}>
          {busy ? 'جارٍ الحفظ...' : editing ? 'حفظ التعديلات' : 'إضافة الأصل'}
        </button>
      </div>
    </div>
  )
}
