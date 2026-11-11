import { useEffect, useState } from 'react'
import { api } from '../api'
import { Confirm, DateInput, Empty, Field, Loading, Modal, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { useLookups } from '../lookups'
import { fmtDate, label } from '../lib'

interface Campaign {
  id: number
  name: string
  departmentId?: number | null
  departmentName?: string | null
  startDate: string
  endDate: string
  status: string
  assetCount: number
  foundCount: number
  notFoundCount: number
  createdAt: string
  createdByName: string
}

interface CampaignItem {
  assetId: number
  assetCode: string
  assetName: string
  isFound?: boolean | null
  notes?: string | null
  recordedAt?: string | null
  recordedByName?: string | null
}

interface Progress {
  campaignId: number
  name: string
  items: CampaignItem[]
  assetCount: number
  recordedCount: number
  missingCount: number
}

export default function Inventory() {
  const { canWrite } = useAuth()
  const toast = useToast()
  const lookups = useLookups()
  const [campaigns, setCampaigns] = useState<Campaign[]>([])
  const [loading, setLoading] = useState(true)
  const [createOpen, setCreateOpen] = useState(false)
  const [progress, setProgress] = useState<Campaign | null>(null)
  const [completeFor, setCompleteFor] = useState<Campaign | null>(null)
  const [cancelFor, setCancelFor] = useState<Campaign | null>(null)
  const [busy, setBusy] = useState(false)

  const reload = () => {
    setLoading(true)
    api
      .get<{ items: Campaign[] }>('/api/inventory', { page: 1, pageSize: 100 })
      .then((d) => setCampaigns(d.items))
      .catch((e) => toast(errText(e), 'error'))
      .finally(() => setLoading(false))
  }

  useEffect(() => {
    reload()
  }, [])

  const act = async (path: string, ok: string, camp?: Campaign | null) => {
    setBusy(true)
    try {
      await api.post(path, {})
      toast(ok)
      if (camp) setProgress(null)
      reload()
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div>
      <div className="page-head">
        <h1>الجرد الدوري</h1>
        {canWrite && (
          <button className="btn btn-primary" onClick={() => setCreateOpen(true)}>
            + حملة جرد جديدة
          </button>
        )}
      </div>

      <div className="card">
        {loading ? (
          <Loading />
        ) : campaigns.length === 0 ? (
          <Empty text="لا توجد حملات جرد" />
        ) : (
          campaigns.map((c) => (
            <div key={c.id} className="card flex-between" style={{ padding: '0.8rem 1rem' }}>
              <div>
                <strong>{c.name}</strong>
                {c.departmentName && <span className="badge blue" style={{ marginInlineStart: 8 }}>{c.departmentName}</span>}
                <div className="muted">
                  {fmtDate(c.startDate)} → {fmtDate(c.endDate)} · {c.assetCount} أصل · وُجد {c.foundCount} · غير موجود {c.notFoundCount}
                </div>
              </div>
              <div className="flex">
                <span className={'badge ' + (c.status === 'Completed' ? 'green' : c.status === 'InProgress' ? 'amber' : c.status === 'Cancelled' ? 'red' : 'grey')}>
                  {label(c.status)}
                </span>
                {canWrite && (
                  <>
                    {c.status === 'InProgress' && <button className="btn btn-sm" onClick={() => setProgress(c)}>متابعة الجرد</button>}
                    {c.status === 'Planned' && (
                      <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => act('/api/inventory/' + c.id + '/start', 'بدأت الحملة')}>
                        بدء
                      </button>
                    )}
                    {c.status === 'InProgress' && (
                      <button className="btn btn-danger btn-sm" disabled={busy} onClick={() => setCompleteFor(c)}>
                        إنهاء
                      </button>
                    )}
                    {(c.status === 'Planned' || c.status === 'InProgress') && (
                      <button className="btn btn-sm" disabled={busy} onClick={() => setCancelFor(c)}>
                        إلغاء
                      </button>
                    )}
                  </>
                )}
              </div>
            </div>
          ))
        )}
      </div>

      {createOpen && (
        <CreateCampaign
          depts={lookups.data?.departments ?? []}
          onClose={() => setCreateOpen(false)}
          onDone={() => {
            setCreateOpen(false)
            reload()
          }}
        />
      )}
      {progress && <ProgressModal campaign={progress} onClose={() => setProgress(null)} onDone={reload} />}
      <Confirm
        open={!!completeFor}
        title="إنهاء الجرد"
        message={'إنهاء حملة «' + (completeFor?.name ?? '') + '»؟ سيتوقف التسجيل بعدها.'}
        onCancel={() => setCompleteFor(null)}
        onConfirm={() => act('/api/inventory/' + completeFor!.id + '/complete', 'تم إنهاء الحملة', completeFor)}
      />
      <Confirm
        open={!!cancelFor}
        title="إلغاء الحملة"
        danger
        message={'إلغاء حملة «' + (cancelFor?.name ?? '') + '»؟'}
        onCancel={() => setCancelFor(null)}
        onConfirm={() => act('/api/inventory/' + cancelFor!.id + '/cancel', 'تم الإلغاء', cancelFor)}
      />
    </div>
  )
}

function CreateCampaign({ depts, onClose, onDone }: { depts: { id: number; name: string; children?: unknown[] }[]; onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [name, setName] = useState('')
  const [deptId, setDeptId] = useState('')
  const [start, setStart] = useState(new Date().toISOString().slice(0, 10))
  const [end, setEnd] = useState('')
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  return (
    <Modal
      open
      onClose={onClose}
      title="حملة جرد جديدة"
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button
            className="btn btn-primary"
            disabled={busy}
            onClick={async () => {
              setErr('')
              if (!name.trim() || !start || !end) return setErr('الاسم وفترة الجرد مطلوبة')
              setBusy(true)
              try {
                await api.post('/api/inventory', { name: name.trim(), departmentId: deptId ? Number(deptId) : null, startDate: start, endDate: end })
                toast('تم إنشاء الحملة')
                onDone()
              } catch (e) {
                setErr(errText(e))
                setBusy(false)
              }
            }}
          >
            إنشاء
          </button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <Field label="اسم الحملة" required>
        <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      <Field label="الإدارة (اختياري — كل الإدارات إذا تُرك)">
        <select className="input" value={deptId} onChange={(e) => setDeptId(e.target.value)}>
          <option value="">كل الإدارات</option>
          {depts.map((d) => (
            <option key={d.id} value={d.id}>{d.name}</option>
          ))}
        </select>
      </Field>
      <div className="grid grid-2">
        <Field label="البداية" required>
          <DateInput value={start} onChange={setStart} />
        </Field>
        <Field label="النهاية" required>
          <DateInput value={end} onChange={setEnd} />
        </Field>
      </div>
    </Modal>
  )
}

function ProgressModal({ campaign, onClose, onDone }: { campaign: Campaign; onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [progress, setProgress] = useState<Progress | null>(null)
  const [saving, setSaving] = useState<number | null>(null)

  const load = () => {
    api.get<Progress>('/api/inventory/' + campaign.id).then(setProgress)
  }
  useEffect(() => {
    load()
  }, [campaign.id])

  const setState = (assetId: number, patch: Partial<CampaignItem>) => {
    setProgress((p) => (p ? { ...p, items: p.items.map((i) => (i.assetId === assetId ? { ...i, ...patch } : i)) } : p))
  }

  const record = async (assetId: number) => {
    const it = progress?.items.find((x) => x.assetId === assetId)
    if (!it || it.isFound === null || it.isFound === undefined) return toast('حدد الحالة أولًا', 'error')
    setSaving(assetId)
    try {
      await api.post('/api/inventory/' + campaign.id + '/record', { assetId, isFound: it.isFound, notes: it.notes || null })
      toast('تم تسجيل القطعة')
      load()
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setSaving(null)
    }
  }

  return (
    <Modal open onClose={onClose} title={'متابعة الجرد: ' + campaign.name} wide>
      {!progress ? (
        <Loading />
      ) : (
        <>
          <div className="kpis">
            <div className="kpi"><div className="n">{progress.assetCount}</div><div className="l">إجمالي الأصول</div></div>
            <div className="kpi good"><div className="n">{progress.items.filter((i) => i.isFound).length}</div><div className="l">تم العثور</div></div>
            <div className="kpi warn"><div className="n">{progress.missingCount}</div><div className="l">غير موجود</div></div>
            <div className="kpi"><div className="n">{progress.items.filter((i) => i.isFound === null || i.isFound === undefined).length}</div><div className="l">لم يُسجل بعد</div></div>
          </div>
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>الأصل</th>
                  <th>الحالة</th>
                  <th>ملاحظات</th>
                  <th>سُجلت بواسطة</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {progress.items.map((it) => (
                  <tr key={it.assetId}>
                    <td>
                      <div>{it.assetCode}</div>
                      <div className="muted">{it.assetName}</div>
                    </td>
                    <td>
                      <div className="flex">
                        <label><input type="radio" name={'f' + it.assetId} checked={it.isFound === true} onChange={() => setState(it.assetId, { isFound: true })} /> موجود</label>
                        <label><input type="radio" name={'f' + it.assetId} checked={it.isFound === false} onChange={() => setState(it.assetId, { isFound: false })} /> غير موجود</label>
                      </div>
                    </td>
                    <td>
                      <input className="input" value={it.notes ?? ''} onChange={(e) => setState(it.assetId, { notes: e.target.value })} />
                    </td>
                    <td className="muted">{it.recordedByName ?? '—'}</td>
                    <td>
                      <button className="btn btn-sm" disabled={saving === it.assetId} onClick={() => record(it.assetId)}>
                        {saving === it.assetId ? '...' : 'حفظ'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>إغلاق</button>
        <button className="btn" onClick={onDone}>تحديث</button>
      </div>
    </Modal>
  )
}
