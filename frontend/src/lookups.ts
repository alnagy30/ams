import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import type { Lookups, TreeNode, Supplier, Accessory } from './types'

let cache: Lookups | null = null
let inflight: Promise<Lookups> | null = null

export async function fetchLookups(force = false): Promise<Lookups> {
  if (cache && !force) return cache
  if (!inflight || force) {
    inflight = api.get<Lookups>('/api/reference/lookups').then((d) => {
      cache = d
      return d
    })
  }
  return inflight
}

export interface LookupsState {
  data: Lookups | null
  error: string
  reload: () => Promise<void>
}

export function useLookups(): LookupsState {
  const [data, setData] = useState<Lookups | null>(cache)
  const [error, setError] = useState('')

  const reload = useCallback(async () => {
    try {
      setData(await fetchLookups(true))
      setError('')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'تعذر تحميل البيانات المرجعية')
    }
  }, [])

  useEffect(() => {
    let mounted = true
    fetchLookups()
      .then((d) => mounted && setData(d))
      .catch((e) => mounted && setError(e instanceof Error ? e.message : 'تعذر تحميل البيانات المرجعية'))
    return () => {
      mounted = false
    }
  }, [])

  return { data, error, reload }
}

export function useTree<T extends TreeNode>(nodes: T[] | undefined, onlyLeaf = false): { value: string; label: string; depth: number }[] {
  if (!nodes) return []
  const out: { value: string; label: string; depth: number }[] = []
  const walk = (list: T[], depth: number) => {
    for (const n of list) {
      const kids = (n.children ?? []) as T[]
      if (!onlyLeaf || kids.length === 0) out.push({ value: String(n.id), label: n.name, depth })
      if (kids.length) walk(kids, depth + 1)
    }
  }
  walk(nodes, 0)
  return out
}

export interface SelOpt {
  value: string
  label: string
}

export function suppliersOpts(s: Supplier[] | undefined): SelOpt[] {
  return (s ?? []).filter((x) => !x.isDeleted).map((x) => ({ value: String(x.id), label: x.name }))
}

export function accessoriesOpts(a: Accessory[] | undefined): SelOpt[] {
  return (a ?? []).map((x) => ({ value: String(x.id), label: x.name }))
}
