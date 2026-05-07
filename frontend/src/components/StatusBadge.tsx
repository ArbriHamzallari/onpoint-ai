import type { IssueStatus } from '../types'

const styles: Record<IssueStatus, string> = {
  open: 'bg-red-50 text-red-600 border border-red-200',
  assigned: 'bg-blue-50 text-blue-600 border border-blue-200',
  in_progress: 'bg-amber-50 text-amber-700 border border-amber-200',
  resolved: 'bg-green-50 text-green-700 border border-green-200',
  cancelled: 'bg-slate-100 text-slate-600 border border-slate-200',
}

const labels: Record<IssueStatus, string> = {
  open: 'Open',
  assigned: 'Assigned',
  in_progress: 'In Progress',
  resolved: 'Resolved',
  cancelled: 'Cancelled',
}

export function StatusBadge({ status }: { status: IssueStatus }) {
  return (
    <span
      className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${styles[status]}`}
    >
      {labels[status]}
    </span>
  )
}
