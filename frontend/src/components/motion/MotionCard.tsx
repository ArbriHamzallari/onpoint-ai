import { motion } from 'framer-motion'
import type { HTMLMotionProps } from 'framer-motion'

interface MotionCardProps extends HTMLMotionProps<'div'> {
  delay?: number
  children: React.ReactNode
}

/**
 * A card surface that fades + slides in on mount.
 * Used as a drop-in for any panel/card that should feel alive.
 */
export function MotionCard({
  delay = 0,
  children,
  className = '',
  ...rest
}: MotionCardProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{
        duration: 0.36,
        delay,
        ease: [0.16, 1, 0.3, 1],
      }}
      className={className}
      {...rest}
    >
      {children}
    </motion.div>
  )
}
