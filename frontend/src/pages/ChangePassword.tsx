import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getSession } from '../api'
import { useAuth } from '../auth'
import { errText } from '../components/ui'

export default function ChangePassword() {
  const { changePassword } = useAuth()
  const nav = useNavigate()
  const forced = getSession()?.user.forcePasswordChange ?? false
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    if (next !== confirm) {
      setError('تأكيد كلمة المرور غير مطابق.')
      return
    }
    if (next.length < 8) {
      setError('كلمة المرور يجب ألا تقل عن 8 أحرف.')
      return
    }
    setBusy(true)
    try {
      await changePassword(current, next)
      nav('/', { replace: true })
    } catch (err) {
      setError(errText(err, 'تعذر تغيير كلمة المرور'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-screen">
      <div className="auth-card">
        <div className="auth-title">تغيير كلمة المرور</div>
        <div className="auth-sub">
          {forced ? 'يجب تغيير كلمة المرور الافتراضية قبل متابعة استخدام النظام.' : 'تحديث كلمة المرور'}
        </div>
        {error && <div className="alert error">{error}</div>}
        <form onSubmit={submit}>
          <div className="field">
            <label className="field-label">كلمة المرور الحالية</label>
            <input className="input" type="password" value={current} autoComplete="current-password" onChange={(e) => setCurrent(e.target.value)} required autoFocus />
          </div>
          <div className="field">
            <label className="field-label">كلمة المرور الجديدة</label>
            <input className="input" type="password" value={next} autoComplete="new-password" onChange={(e) => setNext(e.target.value)} required />
          </div>
          <div className="field">
            <label className="field-label">تأكيد كلمة المرور الجديدة</label>
            <input className="input" type="password" value={confirm} autoComplete="new-password" onChange={(e) => setConfirm(e.target.value)} required />
          </div>
          <button className="btn btn-primary" style={{ width: '100%' }} disabled={busy} type="submit">
            {busy ? 'جارٍ الحفظ...' : 'حفظ'}
          </button>
        </form>
      </div>
    </div>
  )
}
