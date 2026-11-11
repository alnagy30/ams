import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, Field, Loading, Modal, Tabs, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { fmtDateTime, label } from '../lib'
import type { AssetListItem } from '../types'

interface ScrapRequest {
  id: number
  assetId: number
  assetCode: string
  assetName: string
  reason: string
  decision?: string | null
  status: string
  createdAt: string
  createdByName: string
  approvedAt?: string | null
  approvedByName?: string | null
}

export default function Scrap() {
  const { canWrite } = useAuth()
  const toast = useToast()
  const [tab, setTab] = useState('pending')
  const [pending, setPending] = useState<ScrapRequest[]>([])
  const [history, setHistory] = useState<ScrapRequest[]>([])
  const [createOpen, setCreateOpen] = useState(false)
  const [decide, setDecide] = useState<ScrapRequest | null>(null)

  const loadPending = () => {
    api.get<{ items: ScrapRequest[] }>('/api/scrap', { status: 'Pending', page: 1, pageSize: 100 }).then((d) => setPending(d.items))
  }
  const loadHistory = () => {
    api.get<ScrapRequest[]>('/api/scrap/history').then(setHistory)
  }

  useEffect(() => {
    loadPending()
    loadHistory()
  }, [])

  const list = tab === 'pending' ? pending : history
  return (
    <div>
      <div className="page-head">
        <h1>التخريد</h1>
        {canWrite && (
          <button className="btn btn-primary" onClick={() => setCreateOpen(true)}>
            + طلب تخريد
          </button>
        )}
      </div>
      <div className="card">
        <Tabs
          tabs={[
            { key: 'pending', label: 'قيد المراجعة (' + pending.length + ')' },
            { key: 'history', label: 'السجل' }
          ]}
          value={tab}
          onChange={setTab}
        />
        {list.length === 0 ? (
          <Empty text="لا توجد طلبات" />
        ) : (
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>الأصل</th>
                  <th>السبب</th>
                  <th>مقدم الطلب</th>
                  <th>التاريخ</th>
                  <th>الحالة</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {list.map((s) => (
                  <tr key={s.id}>
                    <td>
                      <Link to={'/assets/' + s.assetId}>
                        {s.assetCode} — {s.assetName}
                      </Link>
                    </td>
                    <td className="muted">{s.reason}</td>
                    <td>{s.createdByName}</td>
                    <td>{fmtDateTime(s.createdAt)}</td>
                    <td>
                      <span className={'badge ' + (s.status === 'Pending' ? 'amber' : s.status === 'Approved' ? 'green' : 'red')}>
                        {label(s.status)}
                      </span>
                      {s.approvedByName && <div className="muted">بقرار: {s.approvedByName}</div>}
                    </td>
                    <td>
                      {canWrite && s.status === 'Pending' && (
                        <button className="btn btn-sm" onClick={() => setDecide(s)}>
                          مراجعة
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {createOpen && (
        <CreateScrap
          onClose={() => setCreateOpen(false)}
          onDone={() => {
            setCreateOpen(false)
            loadPending()
            toast('أُرسل طلب التخريد للمراجعة')
          }}
        />
      )}
      {decide && (
        <DecideScrap
          item={decide}
          onClose={() => setDecide(null)}
          onDone={() => {
            setDecide(null)
            loadPending()
            loadHistory()
          }}
        />
      )}
    </div>
  )
}

function CreateScrap({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [assets, setAssets] = useState<AssetListItem[]>([])
  const [assetId, setAssetId] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  useEffect(() => {
    api.get<{ items: AssetListItem[] }>('/api/assets', { page: 1, pageSize: 500 }).then((d) => setAssets(d.items.filter((a) => a.status === 'Active')))
  }, [])

  return (
    <Modal
      open
      onClose={onClose}
      title="طلب تخريد أصل"
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button
            className="btn btn-primary"
            disabled={busy}
            onClick={async () => {
              setErr('')
              if (!assetId || !reason.trim()) return setErr('الأصل والسبب مطلوبان')
              setBusy(true)
              try {
                await api.post('/api/scrap', { assetId: Number(assetId), reason: reason.trim() })
                onDone()
              } catch (e) {
                setErr(errText(e))
                setBusy(false)
              }
            }}
          >
            إرسال الطلب
          </button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <Field label="الأصل" required>
        <select className="input" value={assetId} onChange={(e) => setAssetId(e.target.value)}>
          <option value="">— اختر —</option>
          {assets.map((a) => (
            <option key={a.id} value={a.id}>
              {a.assetCode} — {a.name}
            </option>
          ))}
        </select>
      </Field>
      <Field label="سبب التخريد" required>
        <textarea className="input" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Field>
    </Modal>
  )
}

function DecideScrap({ item, onClose, onDone }: { item: ScrapRequest; onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [decisionText, setDecisionText] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  const submit = async (approve: boolean) => {
    setErr('')
    setBusy(true)
    try {
      await api.post('/api/scrap/decide', { id: item.id, approve, decisionText: decisionText || null })
      toast(approve ? 'تمت الموافقة ونُقل الأصل لسجل التخريد' : 'رُفض الطلب')
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
      title={'مراجعة طلب تخريد: ' + item.assetCode}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إرجاع</button>
          <button className="btn btn-danger" disabled={busy} onClick={() => submit(false)}>رفض</button>
          <button className="btn btn-primary" disabled={busy} onClick={() => submit(true)}>موافقة</button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <div className="alert info">
        <strong>{item.assetName}</strong>
        <div className="muted mt">السبب: {item.reason}</div>
      </div>
      <Field label="نص القرار">
        <textarea className="input" rows={2} value={decisionText} onChange={(e) => setDecisionText(e.target.value)} />
      </Field>
    </Modal>
  )
}
