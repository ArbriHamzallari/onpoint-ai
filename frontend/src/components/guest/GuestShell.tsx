import { motion } from 'framer-motion'
import { Logo } from '../Logo'

interface GuestShellProps {
  /** Optional small caption shown above the main heading (e.g. "Room 204"). */
  eyebrow?: string
  children: React.ReactNode
}

/**
 * Full-screen dark guest surface. Sets the radial-gradient violet hero, sticks
 * the OnPoint logo to the top, and stages a centered max-width column. Every
 * guest screen wraps in this so palette + spacing are consistent.
 *
 * Mobile-first: padded for thumb reach, no horizontal scroll, content vertically
 * centered so single-step screens (G1, G5) feel grounded on small viewports.
 */
export function GuestShell({ eyebrow, children }: GuestShellProps) {
  return (
    <div className="min-h-screen bg-guest-hero text-white font-sans flex flex-col">
      <header className="px-6 pt-6 pb-2 flex-shrink-0">
        <Logo size="sm" variant="light" />
      </header>

      <main className="flex-1 flex items-center justify-center px-6 py-8">
        <motion.div
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.36, ease: [0.16, 1, 0.3, 1] }}
          className="w-full max-w-md"
        >
          {eyebrow && (
            <p className="text-xs uppercase tracking-[0.18em] text-white/50 mb-3">
              {eyebrow}
            </p>
          )}
          {children}
        </motion.div>
      </main>
    </div>
  )
}
