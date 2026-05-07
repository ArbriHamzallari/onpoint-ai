import type { IssuePriority } from '../types'

const styles: Record<IssuePriority, string> = {
  low: 'bg-slate-100 text-slate-600 border border-slate-200',
  medium: 'bg-amber-50 text-amber-700 border border-amber-200',
  high: 'bg-red-50 text-red-600 border border-red-200',
  urgent: 'bg-red-100 text-red-700 border border-red-300',
}

const labels: Record<IssuePriority, string> = {
  low: 'Low',
  medium: 'Medium',
  high: 'High Priority',
  urgent: 'Urgent',
}

export function PriorityBadge({ priority }: { priority: IssuePriority }) {
  return (
    <span
      className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${styles[priority]}`}
    >
      {labels[priority]}
    </span>
  )
}
