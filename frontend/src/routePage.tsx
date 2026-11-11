import { Suspense, lazy } from 'react'
import type { ComponentType, ReactNode } from 'react'

export function page(loader: () => Promise<{ default: ComponentType }>): ReactNode {
  const C = lazy(loader)
  return (
    <Suspense
      fallback={
        <div className="empty">
          <div className="flex" style={{ justifyContent: 'center' }}>
            <span className="spinner lg" /> <span>جارٍ التحميل...</span>
          </div>
        </div>
      }
    >
      <C />
    </Suspense>
  )
}
