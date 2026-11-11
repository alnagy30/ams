import { createContext, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api, clearSession, getSession, request, setSession } from './api'
import type { Session } from './types'

interface AuthApi {
  user: Session['user'] | null
  isAdmin: boolean
  isViewer: boolean
  canWrite: boolean
  hasPerm: (p: string) => boolean
  login: (username: string, password: string) => Promise<Session>
  changePassword: (current: string, next: string) => Promise<void>
  logout: () => Promise<void>
  reload: () => void
}

const Ctx = createContext<AuthApi>(null as unknown as AuthApi)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [tick, setTick] = useState(0)
  const session = getSession()
  const user = session?.user ?? null

  const value = useMemo<AuthApi>(() => {
    const isAdmin = user?.role === 'Admin'
    const isViewer = user?.role === 'Viewer'
    return {
      user,
      isAdmin,
      isViewer,
      canWrite: user != null && !isViewer,
      hasPerm: (p: string) => isAdmin || (user?.permissions ?? []).includes(p),
      login: async (username, password) => {
        const s = await request<Session>('/api/auth/login', {
          method: 'POST',
          body: { username, password },
          skipAuth: true
        })
        setSession(s)
        return s
      },
      changePassword: async (current, next) => {
        const s = await request<Session>('/api/auth/change-password', {
          method: 'POST',
          body: { currentPassword: current, newPassword: next }
        })
        setSession(s)
      },
      logout: async () => {
        try {
          await api.post('/api/auth/logout', {})
        } catch {
          /* ignore */
        }
        clearSession()
        window.location.href = '/login'
      },
      reload: () => setTick((t) => t + 1)
    }
  }, [user, tick])

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}

export function useAuth(): AuthApi {
  return useContext(Ctx)
}
