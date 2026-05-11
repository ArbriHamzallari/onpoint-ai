/**
 * React hook for the guest status screen — subscribes to GuestStatusHub.
 *
 * Auth: cookie-based (no token). The HttpOnly `op_session` cookie travels with
 * the WebSocket negotiate handshake; the hub validates server-side and aborts
 * the connection on missing/expired cookie. We surface that as `connected=false`
 * so the page can fall back to polling and prompt a QR re-scan.
 *
 * Two server events collapse into one `onChanged()` callback — the page
 * always refetches GET /api/feedback/me/issue on either event, since both
 * carry the same single issueId tied to this session.
 */
import { useEffect, useRef, useState } from 'react'
import { HubConnection, HubConnectionState } from '@microsoft/signalr'
import { createGuestStatusHub } from './signalRClient'

interface UseGuestStatusHubOptions {
  /** Invoked on every StatusChanged or AiUpdateAdded event for this session. */
  onChanged: () => void

  /** Set false to skip the connection (e.g. before the session is known). */
  enabled?: boolean
}

export function useGuestStatusHub({
  onChanged,
  enabled = true,
}: UseGuestStatusHubOptions): { connected: boolean } {
  const [connected, setConnected] = useState(false)

  const cbRef = useRef(onChanged)
  useEffect(() => {
    cbRef.current = onChanged
  }, [onChanged])

  useEffect(() => {
    if (!enabled) {
      setConnected(false)
      return
    }

    const hub: HubConnection = createGuestStatusHub()
    let cancelled = false

    const fire = () => cbRef.current()

    hub.on('StatusChanged',  fire)
    hub.on('AiUpdateAdded',  fire)

    hub.onreconnecting(() => !cancelled && setConnected(false))
    hub.onreconnected(() => !cancelled && setConnected(true))
    hub.onclose(() => !cancelled && setConnected(false))

    hub
      .start()
      .then(() => {
        if (!cancelled) setConnected(hub.state === HubConnectionState.Connected)
      })
      .catch((err) => {
        // Most common cause is missing/expired op_session — the hub aborts on
        // OnConnectedAsync. Polling fallback covers this case.
        // eslint-disable-next-line no-console
        console.warn('[useGuestStatusHub] start failed:', err)
      })

    return () => {
      cancelled = true
      setConnected(false)
      hub.stop().catch(() => {})
    }
  }, [enabled])

  return { connected }
}
