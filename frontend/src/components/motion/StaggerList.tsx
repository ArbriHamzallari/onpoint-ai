import { motion } from 'framer-motion'

interface StaggerListProps {
  children: React.ReactNode
  staggerDelay?: number
  className?: string
}

/**
 * Wrap a list to fade-in stagger its direct children on mount.
 * Each child is wrapped in a motion.div automatically.
 */
export function StaggerList({
  children,
  staggerDelay = 0.05,
  className = '',
}: StaggerListProps) {
  const items = Array.isArray(children) ? children : [children]

  return (
    <motion.div
      className={className}
      initial="hidden"
      animate="visible"
      variants={{
        hidden:  {},
        visible: {
          transition: { staggerChildren: staggerDelay },
        },
      }}
    >
      {items.map((child, i) => (
        <motion.div
          key={i}
          variants={{
            hidden:  { opacity: 0, y: 6 },
            visible: { opacity: 1, y: 0 },
          }}
          transition={{
            duration: 0.32,
            ease: [0.16, 1, 0.3, 1],
          }}
        >
          {child}
        </motion.div>
      ))}
    </motion.div>
  )
}
