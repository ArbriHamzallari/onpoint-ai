interface PulseDotProps {
  color?: 'emerald' | 'rose' | 'azure' | 'brand'
  size?: 'sm' | 'md'
}

const colorMap = {
  emerald: 'bg-emerald-500',
  rose:    'bg-rose-500',
  azure:   'bg-azure-500',
  brand:   'bg-brand-600',
}

const sizeMap = {
  sm: 'w-2 h-2',
  md: 'w-2.5 h-2.5',
}

/**
 * A live indicator dot with an animated pulsing ring around it.
 * Pure CSS — no Framer Motion needed.
 */
export function PulseDot({ color = 'emerald', size = 'sm' }: PulseDotProps) {
  return (
    <span className="relative inline-flex items-center justify-center">
      <span
        className={`absolute inline-flex w-full h-full rounded-full ${colorMap[color]} opacity-50 animate-pulse-ring`}
      />
      <span
        className={`relative inline-flex rounded-full ${colorMap[color]} ${sizeMap[size]}`}
      />
    </span>
  )
}
