import React, { createContext, useContext, useState, useCallback } from 'react'
import { setAuthToken } from '../api/client'
import type { AuthResponse } from '../types'

interface AuthState {
  token: string | null
  userId: string | null
  businessId: string | null
  isAuthenticated: boolean
}

interface AuthContextValue extends AuthState {
  login: (response: AuthResponse) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    token: null,
    userId: null,
    businessId: null,
    isAuthenticated: false,
  })

  const login = useCallback((response: AuthResponse) => {
    setAuthToken(response.accessToken)
    setState({
      token: response.accessToken,
      userId: response.userId,
      businessId: response.businessId,
      isAuthenticated: true,
    })
  }, [])

  const logout = useCallback(() => {
    setAuthToken(null)
    setState({
      token: null,
      userId: null,
      businessId: null,
      isAuthenticated: false,
    })
  }, [])

  return (
    <AuthContext.Provider value={{ ...state, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
