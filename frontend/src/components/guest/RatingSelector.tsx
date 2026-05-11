import { motion } from 'framer-motion'
import { useState } from 'react'

interface RatingSelectorProps {
  /** Called with the chosen rating (1-5) when the user taps a star. */
  onSelect: (rating: number) => void
  /** Disables interaction once a rating has been chosen, while we navigate/submit. */
  disabled?: boolean
}

const RATINGS = [1, 2, 3, 4, 5] as const

// Star path — Heroicons-style solid star, filled via currentColor.
function StarPath() {
  return (
    <path
      d="M11.48 3.499a.562.562 0 011.04 0l2.125 5.111a.563.563 0 00.475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 00-.182.557l1.285 5.385a.562.562 0 01-.84.61l-4.725-2.885a.562.562 0 00-.586 0L6.982 20.54a.562.562 0 01-.84-.61l1.285-5.386a.562.562 0 00-.182-.557l-4.204-3.602a.562.562 0 01.321-.988l5.518-.442a.563.563 0 00.475-.345L11.48 3.5z"
      fill="currentColor"
      stroke="currentColor"
      strokeWidth="0.5"
      strokeLinejoin="round"
    />
  )
}

/**
 * The G1 rating control. Five large tappable stars; one is "active" on hover
 * (desktop preview) or after tap (mobile). Tap commits — there's no separate
 * "submit" step on G1 because the rating itself IS the choice.
 *
 * Accessibility: each star is a real <button> with aria-label, focus-visible
 * outline; respects prefers-reduced-motion via framer-motion's MotionConfig
 * higher in the tree (not added here yet — Phase 3c roundup).
 */
export function RatingSelector({ onSelect, disabled = false }: RatingSelectorProps) {
  const [hover, setHover] = useState<number | null>(null)
  const [picked, setPicked] = useState<number | null>(null)

  function handlePick(value: number) {
    if (disabled || picked !== null) return
    setPicked(value)
    onSelect(value)
  }

  const active = picked ?? hover

  return (
    <div
      className="flex items-center justify-center gap-2 sm:gap-3"
      onPointerLeave={() => setHover(null)}
      role="radiogroup"
      aria-label="Rate your experience"
    >
      {RATINGS.map((value, i) => {
        const filled = active !== null && value <= active
        return (
          <motion.button
            key={value}
            type="button"
            disabled={disabled}
            onClick={() => handlePick(value)}
            onPointerEnter={() => setHover(value)}
            aria-label={`${value} star${value === 1 ? '' : 's'}`}
            aria-checked={picked === value}
            role="radio"
            // Staggered entrance — each star appears 60ms after the previous.
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{
              duration: 0.36,
              delay: 0.1 + i * 0.06,
              ease: [0.16, 1, 0.3, 1],
            }}
            whileHover={{ scale: 1.08 }}
            whileTap={{ scale: 0.94 }}
            className={`
              w-12 h-12 sm:w-14 sm:h-14
              flex items-center justify-center
              rounded-full
              transition-colors duration-150
              focus:outline-none focus-visible:ring-2 focus-visible:ring-white/60
              disabled:cursor-not-allowed
              ${filled ? 'text-amber-400' : 'text-white/25'}
            `}
          >
            <svg width="36" height="36" viewBox="0 0 24 24" aria-hidden="true">
              <StarPath />
            </svg>
          </motion.button>
        )
      })}
    </div>
  )
}
