import { useEffect, useState } from 'react'
import { issuesApi } from '../api/issues'
import { departmentsApi } from '../api/departments'
import type { IssueDetail, Department } from '../types'
import { StatusBadge } from './StatusBadge'
import { PriorityBadge } from './PriorityBadge'
import { formatRelativeTime } from '../utils/time'
import { ApiError } from '../api/client'

interface IssueDetailModalProps {
  issueId: string
  onClose: () => void
  onChanged: () => void
}

export function IssueDetailModal({ issueId, onClose, onChanged }: IssueDetailModalProps) {
  const [issue, setIssue] = useState<IssueDetail | null>(null)
  const [departments, setDepartments] = useState<Department[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [issueData, deptData] = await Promise.all([
          issuesApi.get(issueId),
          departmentsApi.list(),
        ])
        if (!cancelled) {
          setIssue(issueData)
          setDepartments(deptData.items)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Failed to load issue')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    return () => {
      cancelled = true
    }
  }, [issueId])

  async function refresh() {
    try {
      const updated = await issuesApi.get(issueId)
      setIssue(updated)
      onChanged()
    } catch {
      // silently ignore — list will refresh on next interval anyway
    }
  }

  async function handleStart() {
    setBusy(true)
    setError(null)
    try {
      await issuesApi.start(issueId)
      await refresh()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to start issue')
    } finally {
      setBusy(false)
    }
  }

  async function handleResolve() {
    setBusy(true)
    setError(null)
    try {
      await issuesApi.resolve(issueId)
      await refresh()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to resolve issue')
    } finally {
      setBusy(false)
    }
  }

  async function handleAssign(departmentId: string) {
    setBusy(true)
    setError(null)
    try {
      await issuesApi.assign(issueId, departmentId)
      await refresh()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign issue')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div
      className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4"
      onClick={onClose}
    >
      <div
        className="bg-card rounded-lg shadow-xl border border-border w-full max-w-lg max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-6 py-4 border-b border-border flex items-center justify-between">
          <h2 className="text-lg font-bold text-text-primary">Issue Details</h2>
          <button
            onClick={onClose}
            className="w-8 h-8 flex items-center justify-center rounded text-text-secondary hover:bg-slate-100"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        <div className="px-6 py-5 space-y-5">
          {loading && <div className="text-text-secondary">Loading…</div>}

          {error && (
            <div className="text-sm text-danger bg-red-50 border border-red-200 rounded-lg px-3 py-2">
              {error}
            </div>
          )}

          {issue && (
            <>
              <div>
                <div className="flex items-center gap-2 mb-2">
                  <span className="font-semibold text-text-primary">
                    {issue.locationName ?? 'Unknown location'}
                  </span>
                  <PriorityBadge priority={issue.priority} />
                  <StatusBadge status={issue.status} />
                </div>
                <h3 className="text-base font-medium text-text-primary">{issue.title}</h3>
                {issue.description && (
                  <p className="text-sm text-text-secondary mt-1">{issue.description}</p>
                )}
              </div>

              <div className="bg-slate-50 rounded-lg p-4 space-y-2">
                <div className="text-sm font-medium text-text-primary">Guest feedback</div>
                <div className="flex items-center gap-1 text-amber-500">
                  {Array.from({ length: 5 }).map((_, i) => (
                    <span key={i} className={i < issue.feedbackRating ? '' : 'opacity-30'}>
                      ★
                    </span>
                  ))}
                  <span className="text-text-secondary text-xs ml-2">
                    {issue.feedbackRating}/5
                  </span>
                </div>
                {issue.feedbackComment && (
                  <p className="text-sm text-text-secondary italic">{`"${issue.feedbackComment}"`}</p>
                )}
              </div>

              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <div className="text-text-secondary text-xs uppercase tracking-wide">Created</div>
                  <div className="text-text-primary">{formatRelativeTime(issue.createdAt)}</div>
                </div>
                <div>
                  <div className="text-text-secondary text-xs uppercase tracking-wide">Department</div>
                  <div className="text-text-primary">{issue.departmentName ?? 'Not assigned'}</div>
                </div>
              </div>

              <div className="space-y-2">
                <label className="text-xs uppercase tracking-wide text-text-secondary font-medium">
                  Assign to department
                </label>
                <select
                  className="w-full px-3 py-2 border border-border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  value={issue.departmentId ?? ''}
                  onChange={(e) => e.target.value && handleAssign(e.target.value)}
                  disabled={busy || issue.status === 'resolved' || issue.status === 'cancelled'}
                >
                  <option value="">Select a department…</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </div>
            </>
          )}
        </div>

        {issue && issue.status !== 'resolved' && issue.status !== 'cancelled' && (
          <div className="px-6 py-4 border-t border-border flex gap-2 justify-end">
            <button
              onClick={handleStart}
              disabled={busy || (issue.status !== 'open' && issue.status !== 'assigned')}
              className="px-4 py-2 text-sm font-medium rounded-lg border border-primary text-primary hover:bg-primary-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Mark in progress
            </button>
            <button
              onClick={handleResolve}
              disabled={busy}
              className="px-4 py-2 text-sm font-medium rounded-lg bg-primary text-white hover:bg-primary-700 transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            >
              Resolve issue
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
