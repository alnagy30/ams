import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth'
import { ToastProvider } from './components/ui'
import { RequireAuth, RequireGuest, MustChangePassword } from './components/Guards'
import { Layout } from './components/Layout'
import { page } from './routePage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ToastProvider>
          <Routes>
            <Route element={<RequireGuest />}>
              <Route path="/login" element={page(() => import('./pages/Login'))} />
            </Route>

            <Route element={<RequireAuth />}>
              <Route path="/change-password" element={page(() => import('./pages/ChangePassword'))} />
              <Route element={<MustChangePassword />}>
                <Route element={<Layout />}>
                  <Route path="/" element={page(() => import('./pages/Dashboard'))} />
                  <Route path="/assets" element={page(() => import('./pages/assets/AssetsList'))} />
                  <Route path="/assets/new" element={page(() => import('./pages/assets/AssetForm'))} />
                  <Route path="/assets/:id" element={page(() => import('./pages/assets/AssetDetail'))} />
                  <Route path="/employees" element={page(() => import('./pages/Employees'))} />
                  <Route path="/custody" element={page(() => import('./pages/Custody'))} />
                  <Route path="/transfers" element={page(() => import('./pages/Transfers'))} />
                  <Route path="/inventory" element={page(() => import('./pages/Inventory'))} />
                  <Route path="/scrap" element={page(() => import('./pages/Scrap'))} />
                  <Route path="/notifications" element={page(() => import('./pages/Notifications'))} />
                  <Route path="/reference" element={page(() => import('./pages/Reference'))} />
                  <Route path="/reports" element={page(() => import('./pages/Reports'))} />
                  <Route path="/import" element={page(() => import('./pages/Import'))} />
                  <Route path="/audit" element={page(() => import('./pages/Audit'))} />
                  <Route path="/users" element={page(() => import('./pages/Users'))} />
                  <Route path="/backup" element={page(() => import('./pages/Backup'))} />
                </Route>
              </Route>
            </Route>

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </ToastProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}
