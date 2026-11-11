import { useEffect, useState } from 'react'

export interface CurrencyDef {
  code: string
  label: string
  symbol: string
  after: boolean
}

export const CURRENCIES: CurrencyDef[] = [
  { code: 'EGP', label: 'جنيه مصري', symbol: 'ج.م', after: true }
]

const KEY = 'bh_currency'
const EVENT = 'bh-currency-changed'
export const DEFAULT_CURRENCY = 'EGP'

function find(code: string | null | undefined): CurrencyDef {
  return CURRENCIES.find((c) => c.code === code) ?? CURRENCIES[0]
}

export function getCurrency(): CurrencyDef {
  try {
    return find(localStorage.getItem(KEY))
  } catch {
    return CURRENCIES[0]
  }
}

export function setCurrency(code: string): void {
  const currency = find(code)
  try {
    localStorage.setItem(KEY, currency.code)
  } catch {
    /* ignore */
  }
  window.dispatchEvent(new CustomEvent(EVENT, { detail: currency }))
}

export function onCurrencyChange(cb: () => void): () => void {
  window.addEventListener(EVENT, cb)
  return () => window.removeEventListener(EVENT, cb)
}

/** React hook that re-renders when the currency changes. */
export function useCurrency(): CurrencyDef {
  const [currency, set] = useState<CurrencyDef>(getCurrency)
  useEffect(() => onCurrencyChange(() => set(getCurrency())), [])
  return currency
}

export function formatMoney(value?: number | null, currency?: CurrencyDef): string {
  if (value === null || value === undefined) return '—'
  const c = currency ?? getCurrency()
  const num = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(value)
  return c.after ? `${num} ${c.symbol}` : `${c.symbol}${num}`
}
