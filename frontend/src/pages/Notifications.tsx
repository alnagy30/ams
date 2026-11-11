import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, Loading, Pagination, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { fmtDateTime, label } from '../lib'
import type { NotificationItem } from '../types'

const PAGE_SIZE = 20

export default function Notifications() {
  const toast = useToast()
  const [items, setItems] = useState<NotificationItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState('Open')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setPage(1)
  }, [status])

  useEffect(() => {
    let live = true
    setLoading(true)
    api
      .get<{ items: NotificationItem[]; totalCount: number }>('/api/notifications', {
        status: status || undefined,
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
  }, [page, status])

  const resolve = async (id: number) => {
    try {
      await api.post('/api/notifications/' + id + '/resolve', {})
      toast('تمت المعالجة')
      setItems(items.filter((x) => x.id !== id))
    } catch (e) {
      toast(errText(e), 'error')
    }
  }

  return (
    <div>
      <div className="page-head">
        <h1>التنبيهات</h1>
      </div>
      <div className="card">
        <div className="toolbar">
          <select className="input" style={{ maxWidth: 180 }} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="Open">المفتوحة</option>
            <option value="Resolved">المعالجة</option>
            <option value="">الكل</option>
          </select>
        </div>
        {loading ? (
          <Loading />
        ) : items.length === 0 ? (
          <Empty text="لا توجد تنبيهات" />
        ) : (
          <>
            {items.map((n) => (
              <div key={n.id} className="flex-between" style={{ padding: '0.6rem 0.2rem', borderBottom: '1px solid var(--border)' }}>
                <div>
                  <div>
                    <span className={'badge ' + (n.status === 'Open' ? 'amber' : 'green')}>{label(n.type)}</span>{' '}
                    {n.relatedEntityType === 'Asset' && n.relatedEntityId ? (
                      <Link to={'/assets/' + n.relatedEntityId}>{n.message}</Link>
                    ) : (
                      n.message
                    )}
                  </div>
                  <div className="muted">{fmtDateTime(n.createdAt)}{n.resolvedByName ? ' · عولجت بواسطة ' + n.resolvedByName : ''}</div>
                </div>
                {n.status === 'Open' && (
                  <button className="btn btn-sm" onClick={() => resolve(n.id)}>
                    معالجة
                  </button>
                )}
              </div>
            ))}
            <Pagination page={page} pageSize={PAGE_SIZE} total={total} onPage={setPage} />
          </>
        )}
      </div>
    </div>
  )
}
