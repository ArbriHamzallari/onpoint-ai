interface StatCardProps {
  label: string
  value: string | number
  caption?: string
  accent: 'red' | 'green' | 'blue' | 'amber'
  icon: React.ReactNode
}

const accentClasses = {
  red: 'bg-red-50 text-red-600',
  green: 'bg-green-50 text-green-600',
  blue: 'bg-blue-50 text-blue-600',
  amber: 'bg-amber-50 text-amber-600',
}

export function StatCard({ label, value, caption, accent, icon }: StatCardProps) {
  return (
    <div className="bg-card rounded-lg border border-border p-5">
      <div className="flex items-start justify-between mb-3">
        <span className="text-sm font-medium text-text-secondary">{label}</span>
        <div
          className={`w-9 h-9 rounded-lg flex items-center justify-center ${accentClasses[accent]}`}
        >
          {icon}
        </div>
      </div>
      <div className="text-3xl font-bold text-text-primary mb-1">{value}</div>
      {caption && <div className="text-xs text-text-secondary">{caption}</div>}
    </div>
  )
}
