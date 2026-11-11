import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { DateInput, Empty, Field, Loading, Modal, Pagination, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { useLookups, useTree } from '../lookups'
import { fmtDate, todayISO } from '../lib'
import type { AssetListItem, Employee, TransferRecord } from '../types'

const PAGE_SIZE = 20

export default function Transfers() {
  const [params] = useSearchParams()
  const [transferOpen, setTransferOpen] = useState(!!params.get('asset'))
  return (
    <div>
      <div className="page-head">
        <h1>الحركات والنقل</h1>
        <TransferButton open={transferOpen} setOpen={setTransferOpen} presetAsset={Number(params.get('asset')) || undefined} />
      </div>
      <History />
    </div>
  )
}

function TransferButton({ open, setOpen, presetAsset }: { open: boolean; setOpen: (v: boolean) => void; presetAsset?: number }) {
  const { canWrite } = useAuth()
  const [modalAsset, setModalAsset] = useState<number | undefined>(presetAsset)
  return (
    <>
      {canWrite && (
        <button className="btn btn-primary" onClick={() => { setModalAsset(presetAsset); setOpen(true) }}>
          + نقل أصل
        </button>
      )}
      {open && <TransferModal presetAsset={modalAsset} onClose={() => setOpen(false)} />}
    </>
  )
}

function History() {
  const [items, setItems] = useState<TransferRecord[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let live = true
    setLoading(true)
    api
      .get<{ items: TransferRecord[]; totalCount: number }>('/api/assets/transfers/history', { page, pageSize: PAGE_SIZE })
      .then((d) => {
        if (!live) return
        setItems(d.items)
        setTotal(d.totalCount)
      })
      .catch(() => {})
      .finally(() => live && setLoading(false))
    return () => {
      live = false
    }
  }, [page])

  return (
    <div className="card">
      {loading ? (
        <Loading />
      ) : items.length === 0 ? (
        <Empty text="لا توجد حركات نقل مسجلة" />
      ) : (
        <>
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>الأصل</th>
                  <th>من إدارة</th>
                  <th>إلى إدارة</th>
                  <th>من موقع</th>
                  <th>إلى موقع</th>
                  <th>التاريخ</th>
                  <th>السبب</th>
                  <th>بواسطة</th>
                </tr>
              </thead>
              <tbody>
                {items.map((t) => (
                  <tr key={t.id}>
                    <td>{t.assetCode} — {t.assetName}</td>
                    <td>{t.fromDepartmentName ?? '—'}</td>
                    <td>{t.toDepartmentName ?? '—'}</td>
                    <td className="muted">{t.fromLocationName ?? '—'}</td>
                    <td className="muted">{t.toLocationName ?? '—'}</td>
                    <td>{fmtDate(t.transferDate)}</td>
                    <td className="muted">{t.reason ?? '—'}</td>
                    <td>{t.createdByName}</td>
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

function TransferModal({ presetAsset, onClose }: { presetAsset?: number; onClose: () => void }) {
  const toast = useToast()
  const lookups = useLookups()
  const [assets, setAssets] = useState<AssetListItem[]>([])
  const [employees, setEmployees] = useState<Employee[]>([])
  const [assetId, setAssetId] = useState(presetAsset ? String(presetAsset) : '')
  const [toDept, setToDept] = useState('')
  const [toLoc, setToLoc] = useState('')
  const [recipient, setRecipient] = useState('')
  const [date, setDate] = useState(todayISO())
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  const eligibleAssets = useMemo(() => assets.filter((a) => a.status === 'Active'), [assets])

  const leafDepts = useMemo(() => useTree(lookups.data?.departments, true), [lookups.data?.departments])

  const leafLocs = useMemo(() => useTree(lookups.data?.locations, true), [lookups.data?.locations])

  useEffect(() => {
    api.get<{ items: AssetListItem[] }>('/api/assets', { page: 1, pageSize: 500, excludeScrapped: true }).then((d) => setAssets(d.items))
    api.get<{ items: Employee[] }>('/api/employees', { page: 1, pageSize: 500 }).then((d) => setEmployees(d.items))
  }, [])

  const submit = async () => {
    setErr('')
    if (!assetId || !toDept || !toLoc) return setErr('الأصل والإدارة والموقع الهدف مطلوبة.')
    setBusy(true)
    try {
      await api.post('/api/assets/transfers/single', {
        assetId: Number(assetId),
        toDepartmentId: Number(toDept),
        toLocationId: Number(toLoc),
        recipientEmployeeId: recipient ? Number(recipient) : null,
        transferDate: date,
        reason: reason || null
      })
      toast('تم تنفيذ النقل بنجاح')
      onClose()
      window.setTimeout(() => window.location.reload(), 600)
    } catch (e) {
      setErr(errText(e))
      setBusy(false)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="نقل أصل"
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button className="btn btn-primary" onClick={submit} disabled={busy}>{busy ? '...' : 'تنفيذ النقل'}</button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <Field label="الأصل" required>
        <select className="input" value={assetId} onChange={(e) => setAssetId(e.target.value)}>
          <option value="">— اختر —</option>
          {eligibleAssets.map((a) => (
            <option key={a.id} value={a.id}>
              {a.assetCode} — {a.name}
            </option>
          ))}
        </select>
      </Field>
      <div className="grid grid-2">
        <Field label="الإدارة الهدف" required>
          <select className="input" value={toDept} onChange={(e) => setToDept(e.target.value)}>
            <option value="">— اختر —</option>
            {leafDepts.map((d) => (
              <option key={d.value} value={d.value}>{d.label}</option>
            ))}
          </select>
        </Field>
        <Field label="الموقع الهدف" required>
          <select className="input" value={toLoc} onChange={(e) => setToLoc(e.target.value)}>
            <option value="">— اختر —</option>
            {leafLocs.map((d) => (
              <option key={d.value} value={d.value}>{d.label}</option>
            ))}
          </select>
        </Field>
      </div>
      <Field label="الموظف المستلم (لنقل العهدة)">
        <select className="input" value={recipient} onChange={(e) => setRecipient(e.target.value)}>
          <option value="">— بدون موظف (عهدة إدارية) —</option>
          {employees.map((e) => (
            <option key={e.id} value={e.id}>
              {e.fullName} — {e.departmentName}
            </option>
          ))}
        </select>
      </Field>
      <div className="grid grid-2">
        <Field label="تاريخ النقل" required>
          <DateInput value={date} onChange={setDate} />
        </Field>
        <Field label="السبب">
          <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
    </Modal>
  )
}
