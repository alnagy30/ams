import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, request } from '../api'
import { Empty, Tabs, errText, useToast } from '../components/ui'
import { fmtDateTime } from '../lib'

interface ImportedAssetRow {
  rowNumber: string
  assetCode: string
  name: string
  mainCategory: string
  subCategory: string
  serialNumber?: string | null
  brand?: string | null
  model?: string | null
  purchaseDate?: string | null
  purchaseValue?: number | null
  supplierName?: string | null
  departmentName: string
  locationName: string
  custodyEmployee?: string | null
  custodyDepartmentGeneral?: string | null
  status?: string | null
  isVehicle: boolean
  plateNumber?: string | null
  chassisNumber?: string | null
  engineNumber?: string | null
  licenseExpiryDate?: string | null
  error?: string | null
}

interface ParseResult {
  fileName: string
  totalRows: number
  validRows: number
  invalidRows: number
  rows: ImportedAssetRow[]
}

interface Batch {
  id: number
  fileName: string
  totalRows: number
  successRows: number
  failedRows: number
  importedAt: string
  importedByName: string
}

interface CreatedAsset {
  id: number
  assetCode: string
  name: string
}

interface ImportSummary {
  total: number
  success: number
  failed: number
  issues: { rowNumber: string; assetCode: string; error: string }[]
  createdAssets: CreatedAsset[]
}

const COLS: { key: keyof ImportedAssetRow; label: string }[] = [
  { key: 'assetCode', label: 'الكود' },
  { key: 'name', label: 'الاسم' },
  { key: 'mainCategory', label: 'التصنيف الرئيسي' },
  { key: 'subCategory', label: 'التصنيف الفرعي' },
  { key: 'departmentName', label: 'الإدارة' },
  { key: 'locationName', label: 'الموقع' },
  { key: 'status', label: 'الحالة' },
  { key: 'custodyEmployee', label: 'عهدة الموظف' },
  { key: 'custodyDepartmentGeneral', label: 'عهدة الإدارة' }
]

const PAGE_SIZE = 50

export default function Import() {
  const toast = useToast()
  const [tab, setTab] = useState('new')
  const [step, setStep] = useState(1)
  const [busy, setBusy] = useState(false)
  const [parse, setParse] = useState<ParseResult | null>(null)
  const [checked, setChecked] = useState<Set<string>>(new Set())
  const [summary, setSummary] = useState<ImportSummary | null>(null)
  const [batches, setBatches] = useState<Batch[]>([])
  const [filter, setFilter] = useState<'all' | 'valid' | 'invalid'>('all')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const loadBatches = useCallback(() => {
    api.get<Batch[]>('/api/import/batches').then(setBatches).catch(() => {})
  }, [])

  useEffect(() => {
    loadBatches()
  }, [loadBatches])

  const parseFile = async (file: File) => {
    setBusy(true)
    setSummary(null)
    setParse(null)
    try {
      const form = new FormData()
      form.append('file', file)
      const res = await request<ParseResult>('/api/import/parse', { method: 'POST', form })
      setParse(res)
      setChecked(new Set(res.rows.filter((r) => !r.error).map((r) => r.rowNumber)))
      setFilter('all')
      setSearch('')
      setPage(1)
      setStep(2)
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  const toggleRow = (rowNumber: string) => {
    setChecked((prev) => {
      const next = new Set(prev)
      if (next.has(rowNumber)) next.delete(rowNumber)
      else next.add(rowNumber)
      return next
    })
  }

  const filteredRows = useMemo(() => {
    const rows = parse?.rows ?? []
    const q = search.trim().toLowerCase()
    return rows.filter((r) => {
      if (filter === 'valid' && r.error) return false
      if (filter === 'invalid' && !r.error) return false
      if (!q) return true
      return [r.assetCode, r.name, r.mainCategory, r.subCategory, r.departmentName, r.locationName, r.serialNumber]
        .some((v) => (v ?? '').toString().toLowerCase().includes(q))
    })
  }, [parse, filter, search])

  const validFiltered = useMemo(() => filteredRows.filter((r) => !r.error), [filteredRows])
  const allVisibleChecked = validFiltered.length > 0 && validFiltered.every((r) => checked.has(r.rowNumber))
  const totalPages = Math.max(1, Math.ceil(filteredRows.length / PAGE_SIZE))
  const pageRows = filteredRows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  const toggleAllVisible = () => {
    setChecked((prev) => {
      const next = new Set(prev)
      if (allVisibleChecked) validFiltered.forEach((r) => next.delete(r.rowNumber))
      else validFiltered.forEach((r) => next.add(r.rowNumber))
      return next
    })
  }

  const errorGroups = useMemo(() => {
    const map = new Map<string, number>()
    for (const r of parse?.rows ?? []) {
      if (!r.error) continue
      map.set(r.error, (map.get(r.error) ?? 0) + 1)
    }
    return [...map.entries()].sort((a, b) => b[1] - a[1])
  }, [parse])

  const selected = (parse?.rows ?? []).filter((r) => checked.has(r.rowNumber))

  const execute = async () => {
    if (selected.length === 0) return
    setBusy(true)
    try {
      const res = await api.post<ImportSummary>('/api/import/execute', {
        rows: selected,
        fileName: parse?.fileName ?? null
      })
      setSummary(res)
      setParse(null)
      loadBatches()
      toast(res.failed === 0 ? 'أُدرج ' + res.success + ' أصلًا بنجاح' : 'أُدرج ' + res.success + ' وفشل ' + res.failed)
      setStep(3)
    } catch (e) {
      toast(errText(e), 'error')
    } finally {
      setBusy(false)
    }
  }

  const resetWizard = () => {
    setParse(null)
    setSummary(null)
    setChecked(new Set())
    setFilter('all')
    setSearch('')
    setPage(1)
    setStep(1)
  }

  const exportErrors = () => {
    const src = parse
      ? parse.rows.filter((r) => r.error).map((r) => ({ ...r, error: r.error ?? '' }))
      : (summary?.issues ?? []).map((s) => ({
          rowNumber: s.rowNumber,
          assetCode: s.assetCode,
          name: '',
          mainCategory: '',
          subCategory: '',
          departmentName: '',
          locationName: '',
          error: s.error
        }))
    if (src.length === 0) return
    const header = ['الصف', 'الكود', 'الاسم', 'التصنيف الرئيسي', 'التصنيف الفرعي', 'الإدارة', 'الموقع', 'سبب الخطأ']
    const lines = src.map((r) => [
      r.rowNumber,
      r.assetCode,
      r.name,
      r.mainCategory,
      r.subCategory,
      r.departmentName,
      r.locationName,
      r.error
    ])
    const csv =
      '\uFEFF' +
      [header, ...lines]
        .map((l) => l.map((c) => '"' + String(c ?? '').replace(/"/g, '""') + '"').join(','))
        .join('\r\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'import-errors-' + new Date().toISOString().slice(0, 10) + '.csv'
    document.body.appendChild(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 4000)
  }

  return (
    <div>
      <div className="page-head">
        <h1>الاستيراد من Excel</h1>
      </div>

      <div className="card">
        <Tabs
          tabs={[
            { key: 'new', label: 'استيراد جديد' },
            { key: 'history', label: 'العمليات السابقة (' + batches.length + ')' }
          ]}
          value={tab}
          onChange={(k) => {
            setTab(k)
            if (k === 'new' && step === 3) resetWizard()
          }}
        />

        {tab === 'new' && (
          <>
            <div className="steps">
              <div className={'step ' + (step === 1 ? 'active' : 'done')}>
                <span className="num">1</span> رفع الملف
              </div>
              <div className={'step ' + (step === 2 ? 'active' : step > 2 ? 'done' : '')}>
                <span className="num">2</span> مراجعة الصفوف
              </div>
              <div className={'step ' + (step === 3 ? 'active' : '')}>
                <span className="num">3</span> النتيجة
              </div>
            </div>

            {step === 1 && (
              <>
                <UploadDropzone busy={busy} onFile={parseFile} />
                <div className="flex-between mt" style={{ alignItems: 'center' }}>
                  <span className="muted">
                    تأكد من أن صفوف الملف تتطابق مع حقول الأصول (الاسم، التصنيف الرئيسي/الفرعي، الإدارة، الموقع...).
                  </span>
                  <a className="btn btn-sm" href="/templates/asset-import-template.xlsx" download="asset-import-template.xlsx">
                    تحميل قالب Excel
                  </a>
                </div>
              </>
            )}

            {step === 2 && parse && (
              <>
                <div className="alert info">
                  <strong>{parse.fileName}</strong> — {parse.totalRows} صفًا · <span className="badge green">صحيح {parse.validRows}</span>{' '}
                  {parse.invalidRows > 0 && <span className="badge red">خاطئ {parse.invalidRows}</span>}
                </div>

                {errorGroups.length > 0 && (
                  <div className="mb">
                    <div className="section-title">ملخص الأخطاء</div>
                    <div className="grid grid-2">
                      {errorGroups.map(([msg, cnt]) => (
                        <div key={msg} className="err-group">
                          <span>{msg}</span>
                          <span className="cnt">{cnt}</span>
                        </div>
                      ))}
                    </div>
                    <button className="btn btn-sm mt" onClick={exportErrors}>
                      تنزيل تقرير الأخطاء (CSV)
                    </button>
                  </div>
                )}

                <div className="filter-bar">
                  <div className="flex">
                    <button className={'btn btn-sm' + (filter === 'all' ? ' btn-primary' : '')} onClick={() => { setFilter('all'); setPage(1) }}>
                      الكل ({parse.rows.length})
                    </button>
                    <button className={'btn btn-sm' + (filter === 'valid' ? ' btn-primary' : '')} onClick={() => { setFilter('valid'); setPage(1) }}>
                      صالح ({parse.validRows})
                    </button>
                    <button className={'btn btn-sm' + (filter === 'invalid' ? ' btn-primary' : '')} onClick={() => { setFilter('invalid'); setPage(1) }}>
                      خاطئ ({parse.invalidRows})
                    </button>
                  </div>
                  <input
                    className="input"
                    style={{ maxWidth: 260 }}
                    placeholder="بحث بالكود أو الاسم..."
                    value={search}
                    onChange={(e) => { setSearch(e.target.value); setPage(1) }}
                  />
                  <span className="muted" style={{ marginInlineStart: 'auto' }}>
                    المحدد: {selected.length}
                  </span>
                  <button className="btn btn-primary" disabled={busy || selected.length === 0} onClick={execute}>
                    استيراد {selected.length} صفًا
                  </button>
                </div>

                <div className="tbl-wrap">
                  <table className="tbl">
                    <thead>
                      <tr>
                        <th style={{ width: 34 }}>
                          <input type="checkbox" checked={allVisibleChecked} onChange={toggleAllVisible} title="تحديد كل الظاهر" />
                        </th>
                        {COLS.map((c) => (
                          <th key={c.key}>{c.label}</th>
                        ))}
                        <th>ملاحظات</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pageRows.map((r) => {
                        const invalid = !!r.error
                        return (
                          <tr key={r.rowNumber} style={invalid ? { background: 'var(--danger-bg, #fdf3f4)' } : undefined}>
                            <td>
                              <input
                                type="checkbox"
                                disabled={invalid}
                                checked={checked.has(r.rowNumber)}
                                onChange={() => toggleRow(r.rowNumber)}
                              />
                            </td>
                            {COLS.map((c) => (
                              <td key={String(c.key)} className="muted" style={{ fontSize: 12 }}>
                                {r[c.key] != null && r[c.key] !== '' ? String(r[c.key]) : '—'}
                              </td>
                            ))}
                            <td style={{ fontSize: 12 }}>
                              {invalid ? <span className="badge red">{r.error}</span> : <span className="badge green">صالح</span>}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>

                {filteredRows.length === 0 && <Empty text="لا توجد صفوف مطابقة للفلتر" />}

                {filteredRows.length > 0 && (
                  <div className="pagination">
                    <span className="muted">
                      عرض {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, filteredRows.length)} من {filteredRows.length}
                    </span>
                    <div className="flex">
                      <button className="btn btn-sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                        السابق
                      </button>
                      <span>صفحة {page} من {totalPages}</span>
                      <button className="btn btn-sm" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                        التالي
                      </button>
                    </div>
                  </div>
                )}

                <div className="flex mt" style={{ gap: '0.5rem' }}>
                  <button className="btn" onClick={() => setStep(1)} disabled={busy}>
                    رجوع
                  </button>
                  <span className="muted" style={{ alignSelf: 'center' }}>
                    الصفوف المعطوبة لن تُستورد، ويمكن إصلاحها في الملف وإعادة رفعه.
                  </span>
                </div>
              </>
            )}

            {step === 3 && summary && <ImportSummaryBlock summary={summary} onExport={exportErrors} onReset={resetWizard} />}
          </>
        )}

        {tab === 'history' &&
          (batches.length === 0 ? (
            <Empty text="لا توجد عمليات استيراد سابقة" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>الملف</th>
                    <th>الوقت</th>
                    <th>بواسطة</th>
                    <th>الإجمالي</th>
                    <th>نجح</th>
                    <th>فشل</th>
                  </tr>
                </thead>
                <tbody>
                  {batches.map((b) => (
                    <tr key={b.id}>
                      <td>{b.fileName}</td>
                      <td>{fmtDateTime(b.importedAt)}</td>
                      <td>{b.importedByName}</td>
                      <td>{b.totalRows}</td>
                      <td><span className="badge green">{b.successRows}</span></td>
                      <td>{b.failedRows > 0 ? <span className="badge red">{b.failedRows}</span> : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
      </div>
    </div>
  )
}

function UploadDropzone({ busy, onFile }: { busy: boolean; onFile: (f: File) => void }) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [drag, setDrag] = useState(false)

  return (
    <div
      className={'dropzone' + (drag ? ' drag' : '') + (busy ? ' disabled' : '')}
      onDragOver={(e) => {
        e.preventDefault()
        setDrag(true)
      }}
      onDragLeave={() => setDrag(false)}
      onDrop={(e) => {
        e.preventDefault()
        setDrag(false)
        const f = e.dataTransfer.files?.[0]
        if (f) onFile(f)
      }}
      onClick={() => inputRef.current?.click()}
      role="button"
    >
      <input
        ref={inputRef}
        type="file"
        accept=".xlsx,.xls"
        hidden
        disabled={busy}
        onChange={(e) => {
          const f = e.target.files?.[0]
          if (f) onFile(f)
          e.target.value = ''
        }}
      />
      <div className="big">
        <svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
          <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
          <path d="m7 9 5-5 5 5M12 4v12" />
        </svg>
      </div>
      <div>{busy ? 'جارٍ تحليل الملف...' : 'اسحب ملف Excel هنا أو اضغط للاختيار'}</div>
      <div className="muted">الصيغة المدعومة: xlsx</div>
    </div>
  )
}

function ImportSummaryBlock({
  summary,
  onExport,
  onReset
}: {
  summary: ImportSummary
  onExport: () => void
  onReset: () => void
}) {
  return (
    <div className="mt">
      <div className="alert info">
        <strong>ملخص الاستيراد:</strong> أُدخل {summary.success} من {summary.total}
        {summary.failed > 0 && <> · فشل {summary.failed}</>}
      </div>

      {summary.createdAssets.length > 0 && (
        <div className="mb">
          <div className="section-title">الأصول المُنشأة</div>
          <div className="flex" style={{ flexWrap: 'wrap', gap: '0.4rem' }}>
            {summary.createdAssets.map((a) => (
              <Link key={a.id} to={'/assets/' + a.id} className="badge blue" style={{ textDecoration: 'none' }}>
                {a.assetCode} · {a.name}
              </Link>
            ))}
          </div>
        </div>
      )}

      {summary.issues.length > 0 && (
        <>
          <button className="btn btn-sm mb" onClick={onExport}>
            تنزيل تقرير الأخطاء (CSV)
          </button>
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>الصف</th>
                  <th>الكود</th>
                  <th>الخطأ</th>
                </tr>
              </thead>
              <tbody>
                {summary.issues.slice(0, 200).map((iss, i) => (
                  <tr key={i}>
                    <td>{iss.rowNumber}</td>
                    <td>{iss.assetCode || '—'}</td>
                    <td className="muted">{iss.error}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {summary.issues.length > 200 && (
            <div className="muted mt">يُعرض أول 200 خطأ من {summary.issues.length}. نزّل التقرير لعرض الكل.</div>
          )}
        </>
      )}

      <div className="modal-actions">
        <button className="btn btn-primary" onClick={onReset}>
          استيراد ملف آخر
        </button>
      </div>
    </div>
  )
}
