/**
 * React hook that subscribes to the IssuesHub for live updates on the staff
 * dashboard. The hub broadcasts four event names; this hook fans them out to a
 * single `onChanged(issueId)` callback so callers don't have to wire all four.
 *
 * Returns a `connected` flag. Callers use it to disable polling fallbacks
 * (`useInterval`) while the hub is alive, then re-enable on disconnect.
 *
 * Auth: relies on the global token set by AuthContext (see api/client.ts).
 * Pass `enabled={false}` while the user is not yet authenticated.
 */
import { useEffect, useRef, useState } from 'react'
import { HubConnection, HubConnectionState } from '@microsoft/signalr'
import { createIssuesHub } from './signalRClient'

interface IssueEventPayload {
  issueId: string
}

interface UseIssuesHubOptions {
  /**
   * Called for every IssueCreated / IssueUpdated / IssueAssigned / IssueResolved
   * event. Receives the changed issue's id (string Guid).
   */
  onChanged: (issueId: string) => void

  /** Gate the connection — typically `isAuthenticated` from AuthContext. */
  enabled?: boolean
}

export function useIssuesHub({ onChanged, enabled = true }: UseIssuesHubOptions): {
  connected: boolean
} {
  const [connected, setConnected] = useState(false)

  // Hold the latest callback in a ref so we don't recreate the connection
  // each time the parent re-renders with a new closure.
  const cbRef = useRef(onChanged)
  useEffect(() => {
    cbRef.current = onChanged
  }, [onChanged])

  useEffect(() => {
    if (!enabled) {
      setConnected(false)
      return
    }

    const hub: HubConnection = createIssuesHub()
    let cancelled = false

    const handler = (payload: IssueEventPayload) => cbRef.current(payload.issueId)

    hub.on('IssueCreated',  handler)
    hub.on('IssueUpdated',  handler)
    hub.on('IssueAssigned', handler)
    hub.on('IssueResolved', handler)

    hub.onreconnecting(() => !cancelled && setConnected(false))
    hub.onreconnected(() => !cancelled && setConnected(true))
    hub.onclose(() => !cancelled && setConnected(false))

    hub
      .start()
      .then(() => {
        if (!cancelled) setConnected(hub.state === HubConnectionState.Connected)
      })
      .catch((err) => {
        // Don't surface — caller's polling fallback covers this case.
        // eslint-disable-next-line no-console
        console.warn('[useIssuesHub] start failed:', err)
      })

    return () => {
      cancelled = true
      setConnected(false)
      // Best-effort stop; ignore errors during teardown.
      hub.stop().catch(() => {})
    }
  }, [enabled])

  return { connected }
}
