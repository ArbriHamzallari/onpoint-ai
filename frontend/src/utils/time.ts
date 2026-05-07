/**
 * Format an ISO date string as a short relative time.
 * Examples: "just now", "2 min ago", "1 hr ago", "3 days ago"
 */
export function formatRelativeTime(iso: string): string {
  const then = new Date(iso).getTime()
  const now = Date.now()
  const sec = Math.max(0, Math.floor((now - then) / 1000))

  if (sec < 30) return 'just now'
  if (sec < 60) return `${sec} sec ago`
  const min = Math.floor(sec / 60)
  if (min < 60) return `${min} min ago`
  const hr = Math.floor(min / 60)
  if (hr < 24) return `${hr} hr ago`
  const days = Math.floor(hr / 24)
  if (days < 30) return `${days} day${days === 1 ? '' : 's'} ago`
  const months = Math.floor(days / 30)
  if (months < 12) return `${months} mo ago`
  return new Date(iso).toLocaleDateString()
}

export function formatTimeOfDay(iso: string): string {
  return new Date(iso).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
  })
}
