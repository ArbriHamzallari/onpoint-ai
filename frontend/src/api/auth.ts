import { api } from './client'
import type { AuthResponse, LoginRequest, RegisterRequest } from '../types'

export const authApi = {
  login: (body: LoginRequest) =>
    api.post<AuthResponse>('/api/auth/staff/login', body, { skipAuth: true }),

  register: (body: RegisterRequest) =>
    api.post<AuthResponse>('/api/auth/staff/register', body, { skipAuth: true }),
}
