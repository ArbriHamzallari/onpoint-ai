/**
 * Phase 5b — minimal G3 status screen.
 *
 * NOT the final design. This is the functional skeleton that proves the
 * real-time wiring works end-to-end. Phase 3 will replace the JSX with the
 * Wise-green + Apple-iOS-native treatment from CLAUDE.md §Frontend Design
 * System (dark Ink background, pulsing dots, framer-motion timeline, etc.).
 *
 * What it does today:
 *   • Fetches GET /api/feedback/me/issue on mount (cookie auth via op_session).
 *   • Renders a 4-step timeline: Submitted → Assigned → In Progress → Resolved.
 *   • Subscribes to /hubs/guest — refetches on StatusChanged or AiUpdateAdded.
 *   • Falls back to 10s polling when the hub is disconnected.
 *   • Handles 404 (no issue for this session — positive feedback) gracefully.
 *   • Handles missing/expired cookie (401) with a prompt to re-scan the QR.
 */
import { useCallback, useEffect, useState } from 'react'
import type { GuestIssueStatus, IssueStatus } from '../types'
import { feedbackApi } from '../api/feedback'
import { ApiError } from '../api/client'
import { useGuestStatusHub } from '../realtime'
import { useInterval } from '../hooks/useInterval'
import { formatRelativeTime } from '../utils/time'

const POLLING_FALLBACK_MS = 10_000

// Timeline stages in narrative order. `cancelled` is treated as terminal and
// shown alongside `resolved` — it's a real status but Phase 3 will give it its
// own visual.
const TIMELINE: { key: IssueStatus; label: string }[] = [
  { key: 'open',        label: 'Submitted'   },
  { key: 'assigned',    label: 'Assigned'    },
  { key: 'in_progress', label: 'In progress' },
  { key: 'resolved',    label: 'Resolved'    },
]

function stageIndex(status: IssueStatus): number {
  const idx = TIMELINE.findIndex((s) => s.key === status)
  return idx === -1 ? 0 : idx
}

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ready'; issue: GuestIssueStatus }
  | { kind: 'no-issue' }            // 404 — positive feedback, nothing to show
  | { kind: 'no-session' }          // 401 — cookie missing or expired
  | { kind: 'error'; message: string }

export function GuestStatusPage() {
  const [state, setState] = useState<LoadState>({ kind: 'loading' })

  const refresh = useCallback(async () => {
    try {
      const issue = await feedbackApi.getMyIssue()
      setState({ kind: 'ready', issue })
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 404) setState({ kind: 'no-issue' })
        else if (err.status === 401) setState({ kind: 'no-session' })
        else setState({ kind: 'error', message: err.message })
      } else {
        setState({ kind: 'error', message: 'Failed to load status' })
      }
    }
  }, [])

  useEffect(() => {
    refresh()
  }, [refresh])

  // Subscribe to the guest hub — refetch on any event tied to our session.
  // Only enable once we have a session (no-session state means cookie missing,
  // hub would just abort the connection).
  const { connected } = useGuestStatusHub({
    onChanged: refresh,
    enabled: state.kind !== 'no-session',
  })

  // Polling fallback when hub disconnected. Pause once issue is resolved so
  // we're not hammering the API for a terminal state.
  const polling = !connected && state.kind === 'ready' && state.issue.status !== 'resolved'
  useInterval(refresh, polling ? POLLING_FALLBACK_MS : null)

  // ── Render branches ────────────────────────────────────────────────────────

  if (state.kind === 'loading') {
    return (
      <Shell>
        <p style={{ color: '#666' }}>Loading status…</p>
      </Shell>
    )
  }

  if (state.kind === 'no-session') {
    return (
      <Shell>
        <h2 style={{ marginTop: 0 }}>Session expired</h2>
        <p>Please scan the QR code in your room again to continue.</p>
      </Shell>
    )
  }

  if (state.kind === 'no-issue') {
    return (
      <Shell>
        <h2 style={{ marginTop: 0 }}>Thanks for your feedback!</h2>
        <p>Your rating has been recorded. There's nothing further to track.</p>
      </Shell>
    )
  }

  if (state.kind === 'error') {
    return (
      <Shell>
        <h2 style={{ marginTop: 0, color: '#c00' }}>Something went wrong</h2>
        <p>{state.message}</p>
        <button onClick={refresh} style={btnStyle}>Try again</button>
      </Shell>
    )
  }

  const { issue } = state
  const currentIdx = stageIndex(issue.status)
  const isResolved = issue.status === 'resolved'
  const isCancelled = issue.status === 'cancelled'

  return (
    <Shell>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 24 }}>
        <span
          style={{
            display: 'inline-block',
            width: 8,
            height: 8,
            borderRadius: '50%',
            background: connected ? '#9FE870' : '#FFB020',
          }}
          aria-label={connected ? 'Live' : 'Reconnecting'}
        />
        <span style={{ fontSize: 12, color: '#666', textTransform: 'uppercase', letterSpacing: '0.06em' }}>
          {connected ? 'Live updates' : 'Reconnecting…'}
        </span>
      </div>

      <h1 style={{ fontSize: 24, fontWeight: 700, margin: '0 0 4px' }}>
        {isResolved ? "All sorted!" : isCancelled ? "Cancelled" : "We're on it"}
      </h1>
      {issue.locationName && (
        <p style={{ color: '#666', margin: '0 0 24px' }}>{issue.locationName}</p>
      )}

      {/* Timeline */}
      <ol style={{ listStyle: 'none', padding: 0, margin: '0 0 32px' }}>
        {TIMELINE.map((step, i) => {
          const reached = i <= currentIdx && !isCancelled
          const isCurrent = i === currentIdx && !isResolved && !isCancelled
          return (
            <li
              key={step.key}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                padding: '10px 0',
                opacity: reached ? 1 : 0.4,
              }}
            >
              <span
                style={{
                  display: 'inline-block',
                  width: 10,
                  height: 10,
                  borderRadius: '50%',
                  background: reached ? '#4A9922' : '#ccc',
                  outline: isCurrent ? '4px solid rgba(159, 232, 112, 0.3)' : 'none',
                }}
              />
              <span style={{ fontWeight: reached ? 600 : 400 }}>{step.label}</span>
            </li>
          )
        })}
      </ol>

      {/* Issue summary */}
      <section style={cardStyle}>
        <div style={labelStyle}>Your message</div>
        <p style={{ margin: 0 }}>{issue.description || issue.title}</p>
      </section>

      {/* AI enrichment */}
      {(issue.aiCategory || issue.departmentName) && (
        <section style={cardStyle}>
          <div style={labelStyle}>AI routed</div>
          {issue.departmentName && (
            <p style={{ margin: '4px 0' }}>
              <strong>{issue.departmentName}</strong> is handling this.
            </p>
          )}
          {issue.aiCategory && (
            <p style={{ margin: '4px 0', color: '#666', fontSize: 14 }}>
              Category: <code>{issue.aiCategory}</code>
              {issue.aiPriorityScore != null && (
                <> · Priority score: <code>{issue.aiPriorityScore}/100</code></>
              )}
            </p>
          )}
          {issue.aiFallback && (
            <p style={{ margin: '4px 0', color: '#666', fontSize: 12, fontStyle: 'italic' }}>
              Routed using fallback rules
            </p>
          )}
        </section>
      )}

      <p style={{ fontSize: 12, color: '#999', marginTop: 24 }}>
        Submitted {formatRelativeTime(issue.createdAt)}
      </p>
    </Shell>
  )
}

// ── Inline styles (Phase 3 replaces with Tailwind + Wise palette) ─────────────

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        minHeight: '100vh',
        background: '#F9FAF7',
        padding: '32px 20px',
        fontFamily: 'Geist, system-ui, sans-serif',
      }}
    >
      <div style={{ maxWidth: 480, margin: '0 auto' }}>{children}</div>
    </div>
  )
}

const cardStyle: React.CSSProperties = {
  background: '#fff',
  border: '1px solid rgba(0,0,0,0.06)',
  borderRadius: 12,
  padding: 16,
  marginBottom: 12,
}

const labelStyle: React.CSSProperties = {
  fontSize: 11,
  textTransform: 'uppercase',
  letterSpacing: '0.06em',
  color: '#666',
  marginBottom: 8,
}

const btnStyle: React.CSSProperties = {
  padding: '10px 16px',
  background: '#1A1A1A',
  color: '#fff',
  border: 'none',
  borderRadius: 10,
  fontWeight: 500,
  cursor: 'pointer',
}
