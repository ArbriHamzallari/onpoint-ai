import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { authApi } from '../api/auth'
import { useAuth } from '../contexts/AuthContext'
import { Logo } from '../components/Logo'
import { ApiError } from '../api/client'

export function LoginPage() {
  const navigate = useNavigate()
  const { login, isAuthenticated } = useAuth()

  const [email, setEmail] = useState('demo@onpoint.ai')
  const [password, setPassword] = useState('demo123')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    if (isAuthenticated) navigate('/dashboard', { replace: true })
  }, [isAuthenticated, navigate])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const response = await authApi.login({ email, password })
      login(response)
      navigate('/dashboard', { replace: true })
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Sign in failed'
      setError(message)
      setSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-surface p-4">
      <div className="w-full max-w-md bg-card rounded-lg shadow-sm border border-border p-8">
        <div className="flex justify-center mb-8">
          <Logo size="md" />
        </div>

        <h1 className="text-2xl font-bold text-center text-text-primary mb-2">Welcome back</h1>
        <p className="text-center text-text-secondary mb-8">Sign in to your workspace</p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-text-primary mb-1.5">Email</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full px-3 py-2.5 border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent"
              placeholder="you@example.com"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-text-primary mb-1.5">Password</label>
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-3 py-2.5 border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent"
              placeholder="••••••••"
            />
          </div>

          {error && (
            <div className="text-sm text-danger bg-red-50 border border-red-200 rounded-lg px-3 py-2">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={submitting}
            className="w-full bg-primary text-white py-2.5 rounded-lg font-medium hover:bg-primary-700 transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
          >
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <p className="text-center text-sm text-text-secondary mt-6">
          Demo credentials prefilled — just click sign in.
        </p>
      </div>
    </div>
  )
}
