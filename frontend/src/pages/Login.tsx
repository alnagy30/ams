import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth'
import { errText } from '../components/ui'

export default function Login() {
  const { login } = useAuth()
  const nav = useNavigate()
  const loc = useLocation()
  const from = (loc.state as { from?: string } | null)?.from ?? '/'
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showPass, setShowPass] = useState(false)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setBusy(true)
    try {
      const s = await login(username.trim(), password)
      if (s.user.forcePasswordChange) {
        nav('/change-password', { replace: true })
      } else {
        nav(from, { replace: true })
      }
    } catch (err) {
      setError(errText(err, 'تعذر تسجيل الدخول'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-screen">
      <div className="auth-card">
        <div className="auth-logo">أ</div>
        <div className="auth-title">منظومة إدارة الأصول الثابتة</div>
        <div className="auth-sub">نظام موحّد لإدارة الأصول والعهد والجرد على شبكة المنشأة</div>
        {error && <div className="alert error">{error}</div>}
        <form onSubmit={submit}>
          <div className="field">
            <label className="field-label">اسم المستخدم</label>
            <input
              className="input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoFocus
              autoComplete="username"
              required
            />
          </div>
          <div className="field">
            <label className="field-label">كلمة المرور</label>
            <div className="input-affix">
              <input
                className="input"
                type={showPass ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
              <button
                type="button"
                className="affix-btn"
                onClick={() => setShowPass((v) => !v)}
                aria-label={showPass ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
                tabIndex={-1}
              >
                {showPass ? 'إخفاء' : 'إظهار'}
              </button>
            </div>
          </div>
          <button className="btn btn-primary auth-submit" disabled={busy} type="submit">
            {busy ? 'جارٍ الدخول...' : 'دخول'}
          </button>
        </form>
        <div className="auth-hint">للدخول الأولي: admin / Admin@123456</div>
      </div>
    </div>
  )
}
