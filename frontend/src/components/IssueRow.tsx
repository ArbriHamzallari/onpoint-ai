import type { IssueSummary } from '../types'
import { StatusBadge } from './StatusBadge'
import { PriorityBadge } from './PriorityBadge'
import { formatRelativeTime } from '../utils/time'

interface IssueRowProps {
  issue: IssueSummary
  onStart: (id: string) => void
  onResolve: (id: string) => void
  onView: (id: string) => void
  busyId: string | null
}

const accentByPriority: Record<string, string> = {
  urgent: 'border-l-red-500',
  high: 'border-l-red-500',
  medium: 'border-l-amber-500',
  low: 'border-l-slate-300',
}

export function IssueRow({ issue, onStart, onResolve, onView, busyId }: IssueRowProps) {
  const isResolved = issue.status === 'resolved' || issue.status === 'cancelled'
  const canStart = issue.status === 'open' || issue.status === 'assigned'
  const isBusy = busyId === issue.issueId

  return (
    <div
      className={`bg-card border border-border border-l-4 ${
        accentByPriority[issue.priority] ?? 'border-l-slate-300'
      } rounded-lg p-4 flex items-start gap-4`}
    >
      <div className="w-10 h-10 rounded-lg bg-slate-100 flex items-center justify-center text-slate-500 flex-shrink-0">
        {isResolved ? <CheckIcon /> : <ChatIcon />}
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 mb-1">
          <span className="font-semibold text-text-primary">
            {issue.locationName ?? 'Unknown location'}
          </span>
          <PriorityBadge priority={issue.priority} />
        </div>
        <div className="text-sm font-medium text-text-primary mb-0.5 truncate">{issue.title}</div>
        {issue.description && (
          <div className="text-sm text-text-secondary truncate">{issue.description}</div>
        )}
      </div>

      <div className="flex flex-col items-end gap-2 flex-shrink-0">
        <div className="text-xs text-text-secondary whitespace-nowrap">
          {formatRelativeTime(issue.createdAt)}
        </div>
        <StatusBadge status={issue.status} />
      </div>

      <div className="flex gap-2 flex-shrink-0">
        {!isResolved && (
          <>
            <button
              onClick={() => onStart(issue.issueId)}
              disabled={!canStart || isBusy}
              className="px-4 py-1.5 text-sm font-medium rounded-lg border border-primary text-primary hover:bg-primary-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Start
            </button>
            <button
              onClick={() => onResolve(issue.issueId)}
              disabled={isBusy}
              className="px-4 py-1.5 text-sm font-medium rounded-lg bg-primary text-white hover:bg-primary-700 transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            >
              Resolve
            </button>
          </>
        )}
        <button
          onClick={() => onView(issue.issueId)}
          className="w-9 h-9 flex items-center justify-center rounded-lg text-text-secondary hover:bg-slate-100 transition-colors"
          aria-label="View details"
        >
          <DotsIcon />
        </button>
      </div>
    </div>
  )
}

function ChatIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="#22C55E"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

function DotsIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
      <circle cx="5" cy="12" r="2" />
      <circle cx="12" cy="12" r="2" />
      <circle cx="19" cy="12" r="2" />
    </svg>
  )
}
