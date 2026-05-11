/**
 * G1 — Guest welcome + rating selection.
 *
 * Flow:
 *   1. Page loads, calls GET /api/sessions/me to render business + location.
 *   2. Guest taps 1-5 stars in RatingSelector.
 *   3. Rating ≤ 3 → navigate to /feedback/comment carrying the rating in state.
 *   4. Rating ≥ 4 → submit immediately with no comment, show a minimal thanks
 *      panel. Phase 3c will replace this with the full G5 (Google review CTA).
 *
 * Error branches:
 *   - 401 (no/invalid op_session cookie) → "scan QR again" prompt.
 *   - 404 (session expired) → same prompt.
 *   - Network error → retry button.
 */
import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { GuestShell } from '../components/guest/GuestShell'
import { RatingSelector } from '../components/guest/RatingSelector'
import { sessionsApi } from '../api/sessions'
import { feedbackApi } from '../api/feedback'
import { ApiError } from '../api/client'
import type { SessionContext } from '../types'

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ready'; ctx: SessionContext }
  | { kind: 'no-session' }
  | { kind: 'error'; message: string }

export function GuestFeedbackPage() {
  const navigate = useNavigate()
  const [state, setState] = useState<LoadState>({ kind: 'loading' })
  const [submitting, setSubmitting] = useState(false)
  const [thanks, setThanks] = useState(false)

  const loadContext = useCallback(async () => {
    setState({ kind: 'loading' })
    try {
      const ctx = await sessionsApi.getMyContext()
      setState({ kind: 'ready', ctx })
    } catch (err) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 404)) {
        setState({ kind: 'no-session' })
      } else {
        setState({
          kind: 'error',
          message: err instanceof ApiError ? err.message : 'Could not load session',
        })
      }
    }
  }, [])

  useEffect(() => {
    loadContext()
  }, [loadContext])

  // ── Rating handler ─────────────────────────────────────────────────────────
  //
  // Splits cleanly on rating: low ratings go to G2 (capture details), high
  // ratings submit silently with the rating only — staff still see the positive
  // signal in their dashboard average, no issue is created.
  async function handleRating(rating: number) {
    if (rating <= 3) {
      navigate('/feedback/comment', { state: { rating } })
      return
    }

    setSubmitting(true)
    try {
      const result = await feedbackApi.submit({
        rating,
        website: '', // honeypot — real users always send empty
      })
      // If the business has a configured Google review link, redirect.
      // Otherwise drop to the local thanks panel (Phase 3c builds the full G5).
      if (result.redirectUrl) {
        window.location.href = result.redirectUrl
        return
      }
      setThanks(true)
    } catch (err) {
      setState({
        kind: 'error',
        message: err instanceof ApiError ? err.message : 'Submission failed',
      })
    } finally {
      setSubmitting(false)
    }
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  if (state.kind === 'loading') {
    return (
      <GuestShell>
        <p className="text-white/60 text-center">Loading…</p>
      </GuestShell>
    )
  }

  if (state.kind === 'no-session') {
    return (
      <GuestShell>
        <h1 className="text-2xl font-bold tracking-tight mb-2">Session expired</h1>
        <p className="text-white/70 leading-relaxed">
          Please scan the QR code in your room again to continue.
        </p>
      </GuestShell>
    )
  }

  if (state.kind === 'error') {
    return (
      <GuestShell>
        <h1 className="text-2xl font-bold tracking-tight mb-2">Something went wrong</h1>
        <p className="text-white/70 mb-6">{state.message}</p>
        <button
          onClick={loadContext}
          className="px-5 py-2.5 rounded-lg bg-white/10 hover:bg-white/20 text-white font-medium transition-colors"
        >
          Try again
        </button>
      </GuestShell>
    )
  }

  // Post-submit thanks (rating ≥ 4 happy path until Phase 3c builds G5).
  if (thanks) {
    return (
      <GuestShell eyebrow={state.ctx.location.name}>
        <motion.div
          initial={{ opacity: 0, scale: 0.96 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.36, ease: [0.16, 1, 0.3, 1] }}
        >
          <h1 className="text-3xl font-bold tracking-tight mb-3">Thank you! ✨</h1>
          <p className="text-white/70 leading-relaxed text-lg">
            Your feedback helps us make {state.ctx.businessName} even better.
          </p>
        </motion.div>
      </GuestShell>
    )
  }

  // Main G1 state — welcome + rating.
  const { ctx } = state
  const eyebrow =
    ctx.location.name + (ctx.location.label ? ` · ${ctx.location.label}` : '')

  return (
    <GuestShell eyebrow={eyebrow}>
      <h1 className="text-3xl sm:text-4xl font-bold tracking-tight leading-[1.1] mb-3">
        How was your stay at{' '}
        <span className="bg-clip-text text-transparent bg-gradient-to-r from-brand-300 to-azure-400">
          {ctx.businessName}
        </span>
        ?
      </h1>
      <p className="text-white/60 mb-10 leading-relaxed">
        Tap a star — it takes 5 seconds and goes straight to the people who can fix it.
      </p>

      <div className="mb-8">
        <RatingSelector onSelect={handleRating} disabled={submitting} />
      </div>

      {submitting && (
        <p className="text-center text-sm text-white/50">Sending…</p>
      )}

      <p className="text-center text-xs text-white/30 mt-12 tracking-wider uppercase">
        100% anonymous · No app needed
      </p>
    </GuestShell>
  )
}
