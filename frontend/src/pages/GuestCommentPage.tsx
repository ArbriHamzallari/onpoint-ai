/**
 * G2 — Comment input. Reached from G1 when rating ≤ 3.
 *
 * Reads the rating from React Router location state. If we arrived here
 * directly without a rating (e.g. someone bookmarked the URL), we send them
 * back to G1 — no comment without a rating.
 *
 * Voice button is visual-only this phase; it focuses the textarea on tap so
 * users get a usable affordance until Phase 9 wires Whisper. The textarea is
 * always present (typed input is always available regardless of voice).
 *
 * Honeypot: hidden `website` field positioned off-screen — real users send "",
 * bots typically fill it. The backend silently drops anything with a non-empty
 * honeypot (CLAUDE.md §Security #13).
 */
import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { GuestShell } from '../components/guest/GuestShell'
import { VoiceButton } from '../components/guest/VoiceButton'
import { feedbackApi } from '../api/feedback'
import { ApiError } from '../api/client'

interface LocationState {
  rating?: number
}

export function GuestCommentPage() {
  const navigate = useNavigate()
  const { state } = useLocation()
  const rating = (state as LocationState | null)?.rating

  const [comment, setComment] = useState('')
  const [website, setWebsite] = useState('') // honeypot — must stay empty
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  // Bookmark-without-rating guard.
  useEffect(() => {
    if (rating === undefined) {
      navigate('/feedback', { replace: true })
    }
  }, [rating, navigate])

  if (rating === undefined) return null

  function handleVoicePress() {
    // Phase 3a: tap-to-focus. Phase 9 will start/stop recording here.
    textareaRef.current?.focus()
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const result = await feedbackApi.submit({
        rating: rating!,
        comment: comment.trim() || undefined,
        website, // sent as-is — backend drops if non-empty
      })
      if (!result.issueId) {
        // Backend created feedback but no issue (e.g. honeypot drop or
        // borderline rating that didn't qualify). Show thanks and stop.
        navigate('/feedback', { replace: true, state: { thanks: true } })
        return
      }
      navigate('/feedback/status')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Submission failed')
      setSubmitting(false)
    }
  }

  // Echo rating with the matching number of filled stars.
  const ratingStars = '★'.repeat(rating) + '☆'.repeat(5 - rating)

  return (
    <GuestShell eyebrow={`Rating: ${ratingStars}`}>
      <motion.h1
        initial={{ opacity: 0, y: 8 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.36, ease: [0.16, 1, 0.3, 1] }}
        className="text-3xl sm:text-4xl font-bold tracking-tight leading-[1.1] mb-3"
      >
        Tell us more
      </motion.h1>
      <motion.p
        initial={{ opacity: 0, y: 8 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.36, delay: 0.06, ease: [0.16, 1, 0.3, 1] }}
        className="text-white/60 mb-10 leading-relaxed"
      >
        What's wrong? Speak or type — staff see this right away.
      </motion.p>

      <form onSubmit={handleSubmit}>
        <div className="flex justify-center mb-8">
          <VoiceButton onPress={handleVoicePress} disabled={submitting} />
        </div>
        <p className="text-center text-xs text-white/40 mb-8 tracking-wider uppercase">
          Tap to speak · or type below
        </p>

        <textarea
          ref={textareaRef}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={2000}
          rows={4}
          placeholder="e.g. The air conditioner isn't working and the room is too warm."
          disabled={submitting}
          className="
            w-full px-4 py-3 mb-4
            bg-white/5 border border-white/10
            rounded-xl text-white placeholder:text-white/30
            resize-none
            focus:outline-none focus:ring-2 focus:ring-brand-400 focus:bg-white/8
            transition-colors
          "
        />

        {/* Honeypot — off-screen but in the DOM so naive bots fill it. NEVER
            use display:none; bots check for that. Position absolute + far
            off-screen is the CLAUDE.md-mandated pattern. No label, no autocomplete. */}
        <input
          type="text"
          name="website"
          value={website}
          onChange={(e) => setWebsite(e.target.value)}
          tabIndex={-1}
          autoComplete="off"
          aria-hidden="true"
          style={{ position: 'absolute', left: '-9999px', width: '1px', height: '1px' }}
        />

        {error && (
          <div className="text-sm text-rose-200 bg-rose-500/10 border border-rose-400/30 rounded-lg px-3 py-2 mb-4">
            {error}
          </div>
        )}

        <motion.button
          type="submit"
          disabled={submitting}
          whileTap={{ scale: 0.97 }}
          transition={{ type: 'spring', stiffness: 500, damping: 30 }}
          className="
            w-full py-3.5 rounded-xl
            bg-brand-fill text-white font-semibold
            shadow-glow hover:shadow-glow-lg
            transition-shadow
            disabled:opacity-60 disabled:cursor-not-allowed
          "
        >
          {submitting ? 'Sending…' : 'Send to staff'}
        </motion.button>

        <button
          type="button"
          onClick={() => navigate('/feedback', { replace: true })}
          disabled={submitting}
          className="
            w-full mt-3 py-2 text-sm text-white/50 hover:text-white/80
            transition-colors
            disabled:opacity-50
          "
        >
          ← Back to rating
        </button>
      </form>
    </GuestShell>
  )
}
