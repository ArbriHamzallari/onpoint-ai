import { motion } from 'framer-motion'
import type { HTMLMotionProps } from 'framer-motion'

type GradientButtonProps = Omit<HTMLMotionProps<'button'>, 'children'> & {
  variant?: 'fill' | 'outline' | 'ghost'
  size?: 'sm' | 'md' | 'lg'
  loading?: boolean
  children: React.ReactNode
}

const sizeMap = {
  sm: 'px-3 py-1.5 text-sm',
  md: 'px-5 py-2.5 text-sm',
  lg: 'px-6 py-3 text-base',
}

const variantMap = {
  fill: `
    bg-brand-fill text-white shadow-glow
    hover:shadow-glow-lg
    disabled:opacity-50 disabled:cursor-not-allowed
  `,
  outline: `
    bg-white text-brand-700 border border-brand-200
    hover:bg-brand-50 hover:border-brand-300
    disabled:opacity-50 disabled:cursor-not-allowed
  `,
  ghost: `
    bg-transparent text-ash-600
    hover:bg-ash-100 hover:text-ash-900
    disabled:opacity-50 disabled:cursor-not-allowed
  `,
}

/**
 * The primary action button. Premium press feel via spring scale.
 */
export function GradientButton({
  variant = 'fill',
  size = 'md',
  loading = false,
  disabled,
  children,
  className = '',
  ...rest
}: GradientButtonProps) {
  return (
    <motion.button
      whileTap={{ scale: 0.97 }}
      transition={{ type: 'spring', stiffness: 500, damping: 30 }}
      disabled={disabled || loading}
      className={`
        inline-flex items-center justify-center gap-2
        font-medium rounded-lg
        transition-all duration-150
        ${sizeMap[size]}
        ${variantMap[variant]}
        ${className}
      `}
      {...rest}
    >
      {loading && (
        <span className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin" />
      )}
      {children}
    </motion.button>
  )
}
