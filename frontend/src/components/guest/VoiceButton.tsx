import { motion } from 'framer-motion'

interface VoiceButtonProps {
  /** Whether the button is currently "listening" (visual state only in 3a). */
  active?: boolean
  /** Tap handler. Phase 9 will wire actual Whisper transcription. */
  onPress: () => void
  disabled?: boolean
}

/**
 * Big circular violet-gradient voice button with pulsing concentric rings.
 *
 * Phase 3a: visual + tap affordance only. The expected behaviour is:
 *   - Tap once → reveals/focuses the textarea below (typed input fallback)
 *   - Phase 9 → tap once to start recording, again to stop, Whisper transcribes
 *
 * The animation uses existing Tailwind keyframes (`pulse-ring`) so it costs
 * nothing extra. Two rings offset by 0.6s make the pulse feel layered without
 * needing JS frame logic.
 */
export function VoiceButton({ active = false, onPress, disabled = false }: VoiceButtonProps) {
  return (
    <div className="relative flex items-center justify-center">
      {/* Pulsing rings — only animate while idle so the button doesn't look broken when "active" */}
      {!active && (
        <>
          <span
            className="absolute w-24 h-24 rounded-full bg-brand-500/40 animate-pulse-ring"
            aria-hidden="true"
          />
          <span
            className="absolute w-24 h-24 rounded-full bg-brand-500/40 animate-pulse-ring"
            style={{ animationDelay: '0.6s' }}
            aria-hidden="true"
          />
        </>
      )}

      <motion.button
        type="button"
        disabled={disabled}
        onClick={onPress}
        whileTap={{ scale: 0.94 }}
        transition={{ type: 'spring', stiffness: 500, damping: 30 }}
        aria-pressed={active}
        aria-label={active ? 'Stop listening' : 'Hold to speak'}
        className={`
          relative z-10
          w-24 h-24 rounded-full
          flex items-center justify-center
          bg-brand-fill shadow-glow-lg
          focus:outline-none focus-visible:ring-4 focus-visible:ring-white/40
          disabled:opacity-40 disabled:cursor-not-allowed
          transition-shadow
        `}
      >
        <MicrophoneIcon />
      </motion.button>
    </div>
  )
}

function MicrophoneIcon() {
  return (
    <svg
      width="36"
      height="36"
      viewBox="0 0 24 24"
      fill="none"
      stroke="white"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {/* Mic capsule */}
      <rect x="9" y="3" width="6" height="12" rx="3" fill="white" stroke="none" />
      {/* Stand */}
      <path d="M5 11a7 7 0 0 0 14 0" />
      <line x1="12" y1="18" x2="12" y2="22" />
      <line x1="9" y1="22" x2="15" y2="22" />
    </svg>
  )
}
