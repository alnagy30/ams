import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { getSession } from '../api'

export function RequireAuth() {
  const session = getSession()
  const loc = useLocation()
  if (!session) return <Navigate to="/login" state={{ from: loc.pathname }} replace />
  return <Outlet />
}

export function MustChangePassword() {
  const session = getSession()
  if (session?.user.forcePasswordChange) return <Navigate to="/change-password" replace />
  return <Outlet />
}

export function RequireGuest() {
  const session = getSession()
  if (session) return <Navigate to="/" replace />
  return <Outlet />
}
