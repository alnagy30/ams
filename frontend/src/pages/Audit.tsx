import { useEffect, useState } from 'react'
import { api } from '../api'
import { Empty, Loading, Modal, Pagination, errText } from '../components/ui'
import { fmtDateTime } from '../lib'
import type { AuditEntry } from '../types'

const PAGE_SIZE = 20

const ACTION_LABELS: Record<string, string> = {
  Create: 'إنشاء',
  Update: 'تعديل',
  Delete: 'حذف',
  Login: 'دخول',
  Logout: 'خروج',
  FailedLogin: 'دخول فاشل',
  AssignCustody: 'منح عهدة',
  Transfer: 'نقل',
  ReturnToStore: 'إرجاع للمخزن',
  ApproveScrap: 'اعتماد تخريد',
  Start: 'بدء',
  Complete: 'إكمال',
  Cancel: 'إلغاء',
  Import: 'استيراد',
  Restore: 'استعادة نسخة'
}

export default function Audit() {
  const [items, setItems] = useState<AuditEntry[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [action, setAction] = useState('')
  const [entity, setEntity] = useState('')
  const [loading, setLoading] = useState(true)
  const [detail, setDetail] = useState<AuditEntry | null>(null)

  useEffect(() => {
    setPage(1)
  }, [action, entity])

  useEffect(() => {
    let live = true
    setLoading(true)
    api
      .get<{ items: AuditEntry[]; totalCount: number }>('/api/audit', {
        action: action || undefined,
        entityType: entity || undefined,
        page,
        pageSize: PAGE_SIZE
      })
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
  }, [page, action, entity])

  const pretty = (raw?: string | null): string => {
    if (!raw) return ''
    try {
      return JSON.stringify(JSON.parse(raw), null, 2)
    } catch {
      return raw
    }
  }

  return (
    <div>
      <div className="page-head">
        <h1>سجل التدقيق</h1>
      </div>
      <div className="card">
        <div className="toolbar">
          <select className="input" style={{ maxWidth: 180 }} value={action} onChange={(e) => setAction(e.target.value)}>
            <option value="">كل الإجراءات</option>
            {Object.entries(ACTION_LABELS).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
          <select className="input" style={{ maxWidth: 180 }} value={entity} onChange={(e) => setEntity(e.target.value)}>
            <option value="">كل الكيانات</option>
            {['Asset', 'Employee', 'Department', 'Category', 'Location', 'Supplier', 'Accessory', 'User', 'InventoryCampaign', 'ScrapRequest', 'CustodyRecord', 'TransferRecord'].map((k) => (
              <option key={k} value={k}>{k}</option>
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
                    <th>الوقت</th>
                    <th>المستخدم</th>
                    <th>الإجراء</th>
                    <th>الكيان</th>
                    <th>المعرّف</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((a) => (
                    <tr key={a.id}>
                      <td>{fmtDateTime(a.timestamp)}</td>
                      <td>{a.userName}</td>
                      <td>{ACTION_LABELS[a.action] ?? a.action}</td>
                      <td>{a.entityType}</td>
                      <td>{a.entityId ?? '—'}</td>
                      <td>
                        {(a.beforeData || a.afterData) && (
                          <button className="btn btn-ghost btn-sm" onClick={() => setDetail(a)}>
                            تفاصيل
                          </button>
                        )}
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

      {detail && (
        <Modal open onClose={() => setDetail(null)} title={'تفاصيل السجل — ' + detail.action} wide>
          <div className="grid grid-2">
            {detail.beforeData && (
              <div>
                <div className="field-label">قبل التغيير</div>
                <pre className="code-block">{pretty(detail.beforeData)}</pre>
              </div>
            )}
            {detail.afterData && (
              <div>
                <div className="field-label">بعد التغيير</div>
                <pre className="code-block">{pretty(detail.afterData)}</pre>
              </div>
            )}
          </div>
          <div className="modal-actions">
            <button className="btn" onClick={() => setDetail(null)}>إغلاق</button>
          </div>
        </Modal>
      )}
    </div>
  )
}
