import { Link } from 'react-router-dom'
import { Logo } from '../components/Logo'

export function NotFoundPage() {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center gap-6 bg-surface">
      <Logo size="lg" />
      <h1 className="text-4xl font-bold text-text-primary">404</h1>
      <p className="text-text-secondary">This page does not exist.</p>
      <Link
        to="/"
        className="px-6 py-2 rounded bg-primary text-white font-medium hover:bg-primary-700 transition-colors"
      >
        Go home
      </Link>
    </div>
  )
}
