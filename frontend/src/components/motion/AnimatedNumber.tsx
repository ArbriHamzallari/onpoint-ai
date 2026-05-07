import { motion, useMotionValue, useTransform, animate } from 'framer-motion'
import { useEffect } from 'react'

interface AnimatedNumberProps {
  value: number
  decimals?: number
  duration?: number
  className?: string
}

/**
 * Animates a number counting up from its previous value to `value`.
 * Uses Framer Motion's `animate()` for smooth easing.
 */
export function AnimatedNumber({
  value,
  decimals = 0,
  duration = 0.8,
  className,
}: AnimatedNumberProps) {
  const motionValue = useMotionValue(0)
  const rounded = useTransform(motionValue, (v) =>
    decimals === 0 ? Math.round(v).toString() : v.toFixed(decimals)
  )

  useEffect(() => {
    const controls = animate(motionValue, value, {
      duration,
      ease: [0.16, 1, 0.3, 1],
    })
    return controls.stop
  }, [value, duration, motionValue])

  return <motion.span className={className}>{rounded}</motion.span>
}
