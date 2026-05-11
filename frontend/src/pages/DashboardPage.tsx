import { useCallback, useEffect, useState } from 'react'
import { StaffLayout } from '../components/StaffLayout'
import { StatCard } from '../components/StatCard'
import { IssueRow } from '../components/IssueRow'
import { IssueDetailModal } from '../components/IssueDetailModal'
import { dashboardApi } from '../api/dashboard'
import { issuesApi } from '../api/issues'
import type { DashboardStats, IssueSummary, IssueStatus } from '../types'
import { useInterval } from '../hooks/useInterval'
import { ApiError } from '../api/client'
import { useIssuesHub, useDashboardHub } from '../realtime'
import { useAuth } from '../contexts/AuthContext'

// Polling fallback interval — engaged ONLY when the SignalR hubs are disconnected.
// While the hubs are live the dashboard updates push-style; no need to poll.
const POLLING_FALLBACK_MS = 10_000

export function DashboardPage() {
  const { isAuthenticated } = useAuth()
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [issues, setIssues] = useState<IssueSummary[]>([])
  const [statusFilter, setStatusFilter] = useState<IssueStatus | ''>('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [openIssueId, setOpenIssueId] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      const [s, i] = await Promise.all([
        dashboardApi.getStats(),
        issuesApi.list({
          status: statusFilter || undefined,
          page: 1,
          pageSize: 25,
        }),
      ])
      setStats(s)
      setIssues(i.items)
      setError(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load dashboard')
    } finally {
      setLoading(false)
    }
  }, [statusFilter])

  useEffect(() => {
    refresh()
  }, [refresh])

  // Live updates — both hubs join group "biz:{businessId}" via the JWT claim,
  // so we receive only events for the authenticated tenant.
  const { connected: issuesConnected } = useIssuesHub({
    onChanged: refresh,
    enabled: isAuthenticated,
  })
  const { connected: dashboardConnected } = useDashboardHub({
    onStatsChanged: refresh,
    enabled: isAuthenticated,
  })

  // Both hubs must be live to skip polling. If either drops we fall back so the
  // dashboard stays accurate during reconnects (CLAUDE.md §Real-Time).
  const hubsLive = issuesConnected && dashboardConnected
  useInterval(refresh, hubsLive ? null : POLLING_FALLBACK_MS)

  async function handleStart(id: string) {
    setBusyId(id)
    try {
      await issuesApi.start(id)
      await refresh()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to start issue')
    } finally {
      setBusyId(null)
    }
  }

  async function handleResolve(id: string) {
    setBusyId(id)
    try {
      await issuesApi.resolve(id)
      await refresh()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to resolve issue')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <StaffLayout title="Dashboard">
      {error && (
        <div className="mb-6 text-sm text-danger bg-red-50 border border-red-200 rounded-lg px-4 py-3">
          {error}
        </div>
      )}

      {/* ── Stat cards ──────────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4 mb-8">
        <StatCard
          label="Active Issues"
          value={stats?.activeIssues ?? '—'}
          caption="Live issues right now"
          accent="red"
          icon={<AlertIcon />}
        />
        <StatCard
          label="Resolved Today"
          value={stats?.resolvedToday ?? '—'}
          caption="Closed in the last 24h"
          accent="green"
          icon={<CheckIcon />}
        />
        <StatCard
          label="Total Sessions"
          value={stats?.totalActiveSessions ?? '—'}
          caption="Active guest sessions"
          accent="blue"
          icon={<UsersIcon />}
        />
        <StatCard
          label="Average Score"
          value={stats ? stats.averageRating.toFixed(1) : '—'}
          caption="Across all feedback"
          accent="amber"
          icon={<StarIcon />}
        />
      </div>

      {/* ── Live feed ───────────────────────────────────────── */}
      <div className="bg-card rounded-lg border border-border">
        <div className="px-5 py-4 border-b border-border flex items-center justify-between">
          <div className="flex items-center gap-3">
            <h2 className="font-bold text-text-primary">Live Issue Feed</h2>
            <span className="flex items-center gap-1.5 text-xs text-text-secondary">
              <span
                className={
                  hubsLive
                    ? 'w-1.5 h-1.5 rounded-full bg-success animate-pulse'
                    : 'w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse'
                }
                aria-label={hubsLive ? 'Live' : 'Reconnecting'}
              />
              {hubsLive ? 'Live' : 'Reconnecting…'}
            </span>
          </div>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as IssueStatus | '')}
            className="text-sm px-3 py-1.5 border border-border rounded-lg bg-card focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="">All statuses</option>
            <option value="open">Open</option>
            <option value="assigned">Assigned</option>
            <option value="in_progress">In Progress</option>
            <option value="resolved">Resolved</option>
            <option value="cancelled">Cancelled</option>
          </select>
        </div>

        <div className="p-5 space-y-3">
          {loading && issues.length === 0 && (
            <div className="text-center text-text-secondary py-12">Loading issues…</div>
          )}
          {!loading && issues.length === 0 && (
            <div className="text-center text-text-secondary py-12">No issues to display.</div>
          )}
          {issues.map((issue) => (
            <IssueRow
              key={issue.issueId}
              issue={issue}
              onStart={handleStart}
              onResolve={handleResolve}
              onView={setOpenIssueId}
              busyId={busyId}
            />
          ))}
        </div>
      </div>

      {openIssueId && (
        <IssueDetailModal
          issueId={openIssueId}
          onClose={() => setOpenIssueId(null)}
          onChanged={refresh}
        />
      )}
    </StaffLayout>
  )
}

function AlertIcon() {
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
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
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
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

function UsersIcon() {
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
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  )
}

function StarIcon() {
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
      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
    </svg>
  )
}
