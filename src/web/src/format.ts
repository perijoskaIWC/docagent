// Display formats used across the pages.

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export const formatDateTime = (iso: string) => new Date(iso).toLocaleString()
export const formatPercent = (fraction: number) => `${Math.round(fraction * 100)}%`
export const formatCost = (usd: number) => `$${usd.toFixed(4)}`
export const formatNumber = (value: number) => value.toLocaleString()
