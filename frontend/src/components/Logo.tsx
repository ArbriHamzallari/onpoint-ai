interface LogoProps {
  size?: 'sm' | 'md' | 'lg'
  /**
   * `dark` (default) — slate text on light backgrounds (staff/auth screens).
   * `light` — white text on dark backgrounds (guest hero screens).
   */
  variant?: 'dark' | 'light'
}

const sizeMap = {
  sm: { icon: 24, text: 'text-lg' },
  md: { icon: 32, text: 'text-2xl' },
  lg: { icon: 40, text: 'text-3xl' },
}

export function Logo({ size = 'md', variant = 'dark' }: LogoProps) {
  const { icon, text } = sizeMap[size]
  // Light variant: white wordmark + brighter ring so it sits comfortably on
  // the violet guest hero. Accent stays azure on both for brand continuity.
  const ringColor   = variant === 'light' ? '#A78BFA' : '#2563EB'
  const accentColor = variant === 'light' ? '#C4B5FD' : '#2563EB'
  const wordmarkClass = variant === 'light' ? 'text-white' : 'text-[#1E293B]'

  return (
    <div className="flex items-center gap-2 select-none">
      <svg
        width={icon}
        height={icon}
        viewBox="0 0 40 40"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <circle cx="20" cy="20" r="18" stroke={ringColor} strokeWidth="3" />
        <circle cx="20" cy="20" r="10" stroke={ringColor} strokeWidth="2.5" />
        <circle cx="20" cy="20" r="3.5" fill={accentColor} />
      </svg>
      <span className={`font-bold ${text} ${wordmarkClass}`}>
        OnPoint <span style={{ color: accentColor }}>AI</span>
      </span>
    </div>
  )
}
