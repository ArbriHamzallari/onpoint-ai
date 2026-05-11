/**
 * Typed SignalR connection factories.
 *
 * `accessTokenFactory` is read on every (re)connect so the freshest JWT is used.
 * Reconnect schedule is short-then-longer: 0, 2s, 5s, 10s, 30s — fast recovery
 * from a flaky network but no flooding the server on a hard outage.
 *
 * Browsers cannot set headers on the initial WebSocket upgrade request, so the
 * token rides as `?access_token=...` (the backend reads it in Program.cs's
 * JwtBearerEvents.OnMessageReceived).
 */
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr'
import { getAuthToken } from '../api/client'

const RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000]

function buildHub(url: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => getAuthToken() ?? '',
    })
    .withAutomaticReconnect(RECONNECT_DELAYS_MS)
    .configureLogging(LogLevel.Warning)
    .build()
}

export function createIssuesHub(): HubConnection {
  return buildHub('/hubs/issues')
}

export function createDashboardHub(): HubConnection {
  return buildHub('/hubs/dashboard')
}

/**
 * Guest status hub — no JWT. The op_session cookie is HttpOnly and rides
 * automatically with the WebSocket negotiate request (same-origin via the Vite
 * proxy + `credentials: 'include'`). The hub validates server-side and aborts
 * the connection if the cookie is missing/expired.
 *
 * We use a separate builder here (no `accessTokenFactory`) to keep the staff
 * factory above clean; staff flows always have a token, guest flows never do.
 */
export function createGuestStatusHub(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hubs/guest', {
      withCredentials: true,
    })
    .withAutomaticReconnect(RECONNECT_DELAYS_MS)
    .configureLogging(LogLevel.Warning)
    .build()
}
