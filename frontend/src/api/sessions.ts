import { api } from './client'
import type { SessionContext } from '../types'

export const sessionsApi = {
  /**
   * Returns the business + location context for the current op_session cookie.
   * Throws ApiError(401) if the cookie is missing/invalid (caller should prompt
   * a QR re-scan), or ApiError(404) if the session expired between the QR scan
   * and the page load.
   */
  getMyContext: () =>
    api.get<SessionContext>('/api/sessions/me', { skipAuth: true }),
}
