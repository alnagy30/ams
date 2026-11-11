import { useEffect, useState } from 'react'
import { api, download, triggerDownload } from '../api'
import { Confirm, Empty, Loading, errText, useToast } from '../components/ui'
import { fmtDateTime, fmtBytes } from '../lib'

interface BackupLog {
  id: number
  fileName: string
  isSuccess: boolean
  errorMessage?: string | null
  createdAt: string
  createdByName?: string | null
  isAutomatic: boolean
  fileSizeBytes?: number | null
}

interface BackupList {
  backups: BackupLog[]
  backupDirectory: string
  retentionDays: number
}

export default function Backup() {
  const toast = useToast()
  const [data, setData] = useState<BackupList | null>(null)
  const [busy, setBusy] = useState(false)
  const [restore, setRestore] = useState<BackupLog | null>(null)
  const [creating, setCreating] = useState(false)
  const [restoring, setRestoring] = useState(false)

  const load = () => {
    api.get<BackupList>('/api/backup').then(setData).catch((e) => toast(errText(e), 'error'))
  }

  useEffect(() => {
    load()
  }, [])

  const create = async () => {
    setCreating(true)
    try {
      await api.post('/api/backup')
      toast('أُنشئت النسخة الاحتياطية بنجاح')
      load()
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setCreating(false)
    }
  }

  const doRestore = async () => {
    if (!restore) return
    setRestoring(true)
    try {
      await api.post('/api/backup/restore', { fileName: restore.fileName })
      toast('استُعيدت البيانات. ستحتاج لتسجيل الدخول من جديد.')
      setTimeout(() => (window.location.href = '/login'), 1200)
    } catch (e) {
      toast(errText(e), 'error')
      setRestoring(false)
    }
    setRestore(null)
  }

  const downloadFile = async (fileName: string) => {
    try {
      triggerDownload(await download('/api/backup/files/' + encodeURIComponent(fileName)))
    } catch (e) {
      toast(errText(e), 'error')
    }
  }

  const latest = data?.backups[0] ?? null

  return (
    <div>
      <div className="page-head">
        <h1>النسخ الاحتياطي</h1>
        <button className="btn btn-primary" disabled={creating || busy} onClick={create}>
          {creating ? 'جارٍ الإنشاء...' : '+ إنشاء نسخة احتياطية'}
        </button>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <div className="field-label">آخر نسخة ناجحة</div>
          {latest && latest.isSuccess ? (
            <div className="flex-between">
              <div>
                <div style={{ fontWeight: 600 }}>{latest.fileName}</div>
                <div className="muted">{fmtDateTime(latest.createdAt)} · {latest.createdByName ?? 'تلقائي'}</div>
              </div>
              <button className="btn btn-sm" onClick={() => downloadFile(latest.fileName)}>
                تنزيل
              </button>
            </div>
          ) : (
            <div className="muted">لا توجد نسخة ناجحة بعد.</div>
          )}
        </div>
        <div className="card">
          <div className="field-label">الإعدادات</div>
          <div className="flex-between">
            <div>
              <div className="muted">مسار التخزين</div>
              <div dir="ltr" style={{ fontSize: 12 }}>{data?.backupDirectory ?? '—'}</div>
            </div>
            <div className="muted" style={{ textAlign: 'left' }}>
              <div>الاحتفاظ بـ {data?.retentionDays ?? 30} يومًا</div>
              <div style={{ fontSize: 12 }}>نسخ ليلية تلقائية</div>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {busy || (!data && <Loading />)}
        {data && data.backups.length === 0 ? (
          <Empty text="لا توجد نسخ احتياطية بعد" />
        ) : (
          data && (
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>الملف</th>
                    <th>التاريخ</th>
                    <th>النوع</th>
                    <th>الحجم</th>
                    <th>بواسطة</th>
                    <th>الحالة</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {data.backups.map((b) => (
                    <tr key={b.id}>
                      <td style={{ fontSize: 12 }} dir="ltr">{b.fileName}</td>
                      <td>{fmtDateTime(b.createdAt)}</td>
                      <td>{b.isAutomatic ? <span className="badge grey">تلقائي</span> : <span className="badge blue">يدوي</span>}</td>
                      <td>{fmtBytes(b.fileSizeBytes)}</td>
                      <td>{b.createdByName ?? '—'}</td>
                      <td>
                        {b.isSuccess ? <span className="badge green">ناجحة</span> : <span className="badge red" title={b.errorMessage ?? ''}>فاشلة</span>}
                      </td>
                      <td>
                        {b.isSuccess && (
                          <div className="row-actions">
                            <button className="btn btn-ghost btn-sm" onClick={() => downloadFile(b.fileName)}>تنزيل</button>
                            <button className="btn btn-sm" onClick={() => setRestore(b)}>استعادة</button>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        )}
      </div>

      <Confirm
        open={!!restore}
        danger
        title="استعادة نسخة احتياطية"
        message={'سيتم استبدال قاعدة البيانات الحالية بالنسخة «' + (restore?.fileName ?? '') + '». هل أنت متأكد؟'}
        confirmText="استعادة"
        busy={restoring}
        onCancel={() => setRestore(null)}
        onConfirm={doRestore}
      />
    </div>
  )
}
