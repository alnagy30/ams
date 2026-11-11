import { useEffect, useMemo, useState } from 'react'
import { api } from '../api'
import { Confirm, DateInput, Empty, Field, Loading, Modal, errText } from '../components/ui'
import { useToast } from '../components/ui'
import { useAuth } from '../auth'
import { PERMISSION_LABELS, ROLE_LABELS, fmtDate } from '../lib'

interface UserRow {
  id: number
  username: string
  role: string
  isLocked: boolean
  forcePasswordChange: boolean
  createdAt: string
  lastLoginAt?: string | null
}

interface TempPerm {
  id: number
  userId: number
  userName: string
  permission: string
  startsAt: string
  endsAt: string
  isRevoked: boolean
}

const ROLES = ['Admin', 'AssetManager', 'Viewer']
const ASSIGNABLE_ROLES = ['AssetManager', 'Viewer']
const ALL_PERMS = Object.keys(PERMISSION_LABELS)

function isActivePerm(p: TempPerm): boolean {
  if (p.isRevoked) return false
  const now = Date.now()
  return new Date(p.startsAt).getTime() <= now && new Date(p.endsAt).getTime() >= now
}

export default function Users() {
  const toast = useToast()
  const { user: me } = useAuth()
  const [users, setUsers] = useState<UserRow[]>([])
  const [perms, setPerms] = useState<TempPerm[]>([])
  const [loading, setLoading] = useState(true)
  const [createOpen, setCreateOpen] = useState(false)
  const [resetFor, setResetFor] = useState<UserRow | null>(null)
  const [permFor, setPermFor] = useState<UserRow | null>(null)
  const [toggleLock, setToggleLock] = useState<UserRow | null>(null)
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')

  const load = () => {
    setLoading(true)
    Promise.all([api.get<UserRow[]>('/api/users'), api.get<TempPerm[]>('/api/users/temp-permissions')])
      .then(([u, p]) => {
        setUsers(u)
        setPerms(p)
      })
      .catch((e) => toast(errText(e), 'error'))
      .finally(() => setLoading(false))
  }

  useEffect(() => {
    load()
  }, [])

  const activePermsByUser = useMemo(() => {
    const map = new Map<number, TempPerm[]>()
    for (const p of perms) {
      if (!isActivePerm(p)) continue
      const list = map.get(p.userId) ?? []
      list.push(p)
      map.set(p.userId, list)
    }
    return map
  }, [perms])

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return users.filter((u) => {
      if (roleFilter && u.role !== roleFilter) return false
      if (statusFilter === 'active' && (u.isLocked || u.forcePasswordChange)) return false
      if (statusFilter === 'locked' && !u.isLocked) return false
      if (statusFilter === 'forceChange' && !u.forcePasswordChange) return false
      if (q && !u.username.toLowerCase().includes(q)) return false
      return true
    })
  }, [users, search, roleFilter, statusFilter])

  const changeRole = async (u: UserRow, role: string) => {
    if (role === u.role) return
    try {
      await api.put('/api/users/' + u.id, { role, isLocked: u.isLocked })
      toast('تم تحديث دور ' + u.username)
      load()
    } catch (e) {
      toast(errText(e), 'error')
    }
  }

  return (
    <div>
      <div className="page-head">
        <h1>المستخدمون والأدوار</h1>
        <button className="btn btn-primary" onClick={() => setCreateOpen(true)}>
          + مستخدم جديد
        </button>
      </div>

      <div className="card">
        {loading ? (
          <Loading />
        ) : (
          <>
            <div className="filter-bar">
              <input
                className="input"
                style={{ maxWidth: 240 }}
                placeholder="بحث باسم المستخدم..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <select className="input" style={{ maxWidth: 180 }} value={roleFilter} onChange={(e) => setRoleFilter(e.target.value)}>
                <option value="">كل الأدوار</option>
                {ROLES.map((r) => (
                  <option key={r} value={r}>{ROLE_LABELS[r]}</option>
                ))}
              </select>
              <select className="input" style={{ maxWidth: 200 }} value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
                <option value="">كل الحالات</option>
                <option value="active">نشط</option>
                <option value="forceChange">يجب تغيير كلمة المرور</option>
                <option value="locked">مقفل</option>
              </select>
              <span className="muted" style={{ marginInlineStart: 'auto' }}>
                {filtered.length} من {users.length}
              </span>
            </div>

            {filtered.length === 0 ? (
              <Empty text="لا يوجد مستخدمون مطابقون" />
            ) : (
              <div className="tbl-wrap">
                <table className="tbl">
                  <thead>
                    <tr>
                      <th>اسم المستخدم</th>
                      <th>الدور</th>
                      <th>الحالة</th>
                      <th>الأذونات المؤقتة</th>
                      <th>آخر دخول</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map((u) => {
                      const active = activePermsByUser.get(u.id) ?? []
                      const isMe = u.id === me?.id
                      return (
                        <tr key={u.id}>
                          <td>
                            {u.username}
                            {isMe && <span className="badge blue" style={{ marginInlineStart: 6 }}>أنت</span>}
                          </td>
                          <td>
                            {u.role === 'Admin' || isMe ? (
                              <span className={'badge ' + (u.role === 'Admin' ? 'red' : 'blue')}>
                                {ROLE_LABELS[u.role] ?? u.role}
                              </span>
                            ) : (
                              <select
                                className="input"
                                style={{ padding: '0.2rem 0.4rem', fontSize: '0.8rem' }}
                                value={u.role}
                                onChange={(e) => changeRole(u, e.target.value)}
                              >
                                {ASSIGNABLE_ROLES.map((r) => (
                                  <option key={r} value={r}>{ROLE_LABELS[r]}</option>
                                ))}
                              </select>
                            )}
                          </td>
                          <td>
                            {u.isLocked ? (
                              <span className="badge red">مقفل</span>
                            ) : u.forcePasswordChange ? (
                              <span className="badge amber">يجب تغيير كلمة المرور</span>
                            ) : (
                              <span className="badge green">نشط</span>
                            )}
                          </td>
                          <td>
                            {active.length === 0 ? (
                              <span className="muted">—</span>
                            ) : (
                              <div className="flex" style={{ flexWrap: 'wrap', gap: 4 }}>
                                {active.slice(0, 2).map((p) => (
                                  <span key={p.id} className="badge blue">
                                    {PERMISSION_LABELS[p.permission] ?? p.permission}
                                  </span>
                                ))}
                                {active.length > 2 && <span className="badge grey">+{active.length - 2}</span>}
                              </div>
                            )}
                          </td>
                          <td className="muted">{u.lastLoginAt ? fmtDate(u.lastLoginAt) : 'لم يسجل دخول'}</td>
                          <td>
                            <div className="row-actions">
                              <button className="btn btn-ghost btn-sm" onClick={() => setPermFor(u)}>الأذونات المؤقتة</button>
                              <button className="btn btn-ghost btn-sm" onClick={() => setResetFor(u)}>إعادة تعيين</button>
                              {!isMe && (
                                <button className="btn btn-sm" onClick={() => setToggleLock(u)}>
                                  {u.isLocked ? 'فتح' : 'قفل'}
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>

      {createOpen && (
        <CreateUser
          onClose={() => setCreateOpen(false)}
          onDone={() => {
            setCreateOpen(false)
            load()
          }}
        />
      )}
      {resetFor && (
        <ResetPasswordModal
          user={resetFor}
          onClose={() => setResetFor(null)}
          onDone={() => {
            setResetFor(null)
            load()
          }}
        />
      )}
      {permFor && (
        <TempPermModal
          user={permFor}
          grants={perms.filter((p) => p.userId === permFor.id && !p.isRevoked)}
          onClose={() => setPermFor(null)}
          onDone={() => {
            setPermFor(null)
            load()
          }}
        />
      )}
      <Confirm
        open={!!toggleLock}
        title="قفل / فتح حساب"
        message={(toggleLock?.isLocked ? 'فتح حساب «' : 'قفل حساب «') + (toggleLock?.username ?? '') + '»؟'}
        onCancel={() => setToggleLock(null)}
        onConfirm={async () => {
          try {
            await api.put('/api/users/' + toggleLock!.id, { role: toggleLock!.role, isLocked: !toggleLock!.isLocked })
            toast('تم التحديث')
            load()
          } catch (e) {
            toast(errText(e), 'error')
          }
          setToggleLock(null)
        }}
      />
    </div>
  )
}

function CreateUser({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('AssetManager')
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  return (
    <Modal
      open
      onClose={onClose}
      title="مستخدم جديد"
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button
            className="btn btn-primary"
            disabled={busy}
            onClick={async () => {
              setErr('')
              if (!username.trim() || password.length < 8) return setErr('اسم المستخدم مطلوب وكلمة المرور 8 أحرف على الأقل')
              setBusy(true)
              try {
                await api.post('/api/users', { username: username.trim(), password, role })
                toast('أُنشئ المستخدم')
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
      <Field label="اسم المستخدم" required>
        <input className="input" value={username} onChange={(e) => setUsername(e.target.value)} />
      </Field>
      <Field label="كلمة المرور" required>
        <input className="input" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
      </Field>
      <Field label="الدور">
        <select className="input" value={role} onChange={(e) => setRole(e.target.value)}>
          {ASSIGNABLE_ROLES.map((r) => (
            <option key={r} value={r}>{ROLE_LABELS[r]}</option>
          ))}
        </select>
      </Field>
    </Modal>
  )
}

function ResetPasswordModal({ user, onClose, onDone }: { user: UserRow; onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const [password, setPassword] = useState('')
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  return (
    <Modal
      open
      onClose={onClose}
      title={'إعادة تعيين كلمة مرور ' + user.username}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>إلغاء</button>
          <button
            className="btn btn-primary"
            disabled={busy || password.length < 8}
            onClick={async () => {
              setErr('')
              setBusy(true)
              try {
                await api.post('/api/users/reset-password', { userId: user.id, newPassword: password })
                toast('أُعيد تعيين كلمة المرور')
                onDone()
              } catch (e) {
                setErr(errText(e))
                setBusy(false)
              }
            }}
          >
            تعيين
          </button>
        </>
      }
    >
      {err && <div className="alert error">{err}</div>}
      <div className="muted mb">سيُطلب من المستخدم تغيير كلمة المرور عند الدخول التالي، وستنتهي جلساته الحالية.</div>
      <Field label="كلمة المرور الجديدة (8 أحرف فأكثر)" required>
        <input className="input" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoFocus />
      </Field>
    </Modal>
  )
}

function TempPermModal({ user, grants, onClose, onDone }: { user: UserRow; grants: TempPerm[]; onClose: () => void; onDone: () => void }) {
  const toast = useToast()
  const granted = grants.map((g) => g.permission)
  const available = ALL_PERMS.filter((p) => !granted.includes(p))
  const [perm, setPerm] = useState(available[0] ?? '')
  const [start, setStart] = useState(new Date().toISOString().slice(0, 10))
  const [end, setEnd] = useState('')
  const [busy, setBusy] = useState(false)
  const [toRevoke, setToRevoke] = useState<TempPerm | null>(null)

  return (
    <Modal open onClose={onClose} title={'الأذونات المؤقتة — ' + user.username} wide>
      {grants.length === 0 && <div className="muted mb">لا توجد أذونات مؤقتة ممنوحة.</div>}
      {grants.map((g) => (
        <div key={g.id} className="flex-between" style={{ padding: '0.4rem 0.1rem', borderBottom: '1px dashed var(--border)' }}>
          <div>
            <span className={'badge ' + (isActivePerm(g) ? 'green' : 'grey')}>
              {PERMISSION_LABELS[g.permission] ?? g.permission}
            </span>
            <span className="muted"> من {fmtDate(g.startsAt)} إلى {fmtDate(g.endsAt)}</span>
            {!isActivePerm(g) && <span className="muted"> (غير فعّال)</span>}
          </div>
          {isActivePerm(g) && (
            <button className="btn btn-danger btn-sm" onClick={() => setToRevoke(g)}>إلغاء</button>
          )}
        </div>
      ))}

      {available.length > 0 && (
        <div className="field mt">
          <div className="field-label">منح إذن جديد</div>
          <div className="grid grid-2">
            <select className="input" value={perm} onChange={(e) => setPerm(e.target.value)}>
              {available.map((p) => (
                <option key={p} value={p}>{PERMISSION_LABELS[p] ?? p}</option>
              ))}
            </select>
            <div style={{ display: 'flex', gap: 6 }}>
              <DateInput value={start} onChange={setStart} />
              <DateInput value={end} onChange={setEnd} placeholder="تاريخ الانتهاء" />
            </div>
          </div>
          <button
            className="btn btn-primary btn-sm mt"
            disabled={busy || !perm || !end}
            onClick={async () => {
              setBusy(true)
              try {
                await api.post('/api/users/temp-permissions', { userId: user.id, permission: perm, startsAt: start, endsAt: end })
                toast('مُنح الإذن المؤقت')
                onDone()
              } catch (e) {
                toast(errText(e), 'error')
                setBusy(false)
              }
            }}
          >
            منح
          </button>
        </div>
      )}

      <div className="modal-actions">
        <button className="btn" onClick={onClose}>إغلاق</button>
      </div>

      <Confirm
        open={!!toRevoke}
        title="إلغاء إذن مؤقت"
        message={'إلغاء الإذن ' + (toRevoke ? PERMISSION_LABELS[toRevoke.permission] ?? toRevoke.permission : '') + '؟'}
        danger
        onCancel={() => setToRevoke(null)}
        onConfirm={async () => {
          try {
            await api.del('/api/users/temp-permissions/' + toRevoke!.id)
            toast('أُلغي الإذن')
            onDone()
          } catch (e) {
            toast(errText(e), 'error')
          }
          setToRevoke(null)
        }}
      />
    </Modal>
  )
}
