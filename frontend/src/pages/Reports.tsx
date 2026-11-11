import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, download, triggerDownload } from '../api'
import { Empty, Field, Loading, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { useCurrency, formatMoney } from '../currency'
import type { ReportResult, ReportTableDef } from '../types'

export default function Reports() {
  const { user } = useAuth()
  const toast = useToast()
  const currency = useCurrency()
  const canSeeFin = user?.role === 'Admin' || (user?.permissions ?? []).includes('FinancialValue')

  const [tables, setTables] = useState<ReportTableDef[]>([])
  const [table, setTable] = useState('')
  const [cols, setCols] = useState<string[]>([])
  const [result, setResult] = useState<ReportResult | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.get<{ tables: ReportTableDef[] }>('/api/reports/tables').then((d) => {
      setTables(d.tables)
      if (d.tables.length) setTable(d.tables[0].key)
    })
  }, [])

  const fields = useMemo(() => tables.find((t) => t.key === table)?.fields ?? [], [tables, table])
  const visibleFields = useMemo(() => fields.filter((f) => canSeeFin || !f.financial), [fields, canSeeFin])

  const switchTable = (key: string) => {
    setTable(key)
    setResult(null)
  }

  useEffect(() => {
    const f = tables.find((t) => t.key === table)?.fields ?? []
    setCols(f.filter((c) => canSeeFin || !c.financial).map((c) => c.key))
  }, [table, tables, canSeeFin])

  const toggle = (key: string) => {
    setCols((prev) => (prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key]))
  }

  const run = async () => {
    if (!table || cols.length === 0) return
    setBusy(true)
    try {
      setResult(await api.post<ReportResult>('/api/reports/run', { table, columns: cols, filters: {} }))
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  const exportFile = async (format: 'excel' | 'pdf') => {
    setBusy(true)
    try {
      triggerDownload(
        await download('/api/reports/export?format=' + format, {
          method: 'POST',
          body: { table, columns: cols, filters: {} }
        })
      )
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div>
      <div className="page-head">
        <h1>التقارير</h1>
      </div>
      <div className="card">
        <Field label="الجدول">
          <select className="input" style={{ maxWidth: 300 }} value={table} onChange={(e) => switchTable(e.target.value)}>
            {tables.map((t) => (
              <option key={t.key} value={t.key}>
                {t.label}
              </option>
            ))}
          </select>
        </Field>
        <div className="field">
          <div className="field-label">الأعمدة</div>
          <div className="checkbox-row">
            {visibleFields.map((f) => (
              <label key={f.key}>
                <input type="checkbox" checked={cols.includes(f.key)} onChange={() => toggle(f.key)} />
                {f.label}
              </label>
            ))}
          </div>
          {!canSeeFin && fields.some((f) => f.financial) && <div className="muted">الأعمدة المالية متاحة للمسؤولين فقط.</div>}
        </div>
        <div className="flex">
          <button className="btn btn-primary" disabled={busy || cols.length === 0} onClick={run}>
            تشغيل التقرير
          </button>
          <button className="btn" disabled={busy || cols.length === 0} onClick={() => exportFile('excel')}>
            تصدير Excel
          </button>
          <button className="btn" disabled={busy || cols.length === 0} onClick={() => exportFile('pdf')}>
            تصدير PDF
          </button>
        </div>
      </div>

      {busy && !result && <Loading text="جارٍ إنشاء التقرير..." />}

      {result && (
        <div className="card">
          {result.rows.length === 0 ? (
            <Empty text="لا توجد نتائج" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    {result.columns.map((c) => (
                      <th key={c.key}>{c.label}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {result.rows.map((r, i) => (
                    <tr key={i}>
                      {r.values.map((v, j) => (
                        <td key={j}>
                          {v === null || v === undefined ? '—' : typeof v === 'number' && (result.columns[j]?.dataType === 'number' || result.columns[j]?.dataType === 'decimal')
                            ? result.columns[j]?.financial
                              ? formatMoney(Number(v), currency)
                              : Number(v).toLocaleString('en-US')
                            : String(v)}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="muted mt">إجمالي النتائج: {result.rows.length}</div>
        </div>
      )}
    </div>
  )
}
