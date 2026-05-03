interface LogoProps {
  size?: 'sm' | 'md' | 'lg'
}

const sizeMap = {
  sm: { icon: 24, text: 'text-lg' },
  md: { icon: 32, text: 'text-2xl' },
  lg: { icon: 40, text: 'text-3xl' },
}

export function Logo({ size = 'md' }: LogoProps) {
  const { icon, text } = sizeMap[size]
  return (
    <div className="flex items-center gap-2 select-none">
      <svg
        width={icon}
        height={icon}
        viewBox="0 0 40 40"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <circle cx="20" cy="20" r="18" stroke="#2563EB" strokeWidth="3" />
        <circle cx="20" cy="20" r="10" stroke="#2563EB" strokeWidth="2.5" />
        <circle cx="20" cy="20" r="3.5" fill="#2563EB" />
      </svg>
      <span className={`font-bold ${text} text-[#1E293B]`}>
        OnPoint <span className="text-[#2563EB]">AI</span>
      </span>
    </div>
  )
}
