/**
 * React hook that subscribes to DashboardHub for stat-card invalidation.
 * The hub broadcasts a single `StatsChanged` event (no payload); the caller
 * is expected to refetch GET /api/dashboard/stats on receipt.
 *
 * Mirror shape of useIssuesHub — see that file for rationale on the
 * connection lifecycle.
 */
import { useEffect, useRef, useState } from 'react'
import { HubConnection, HubConnectionState } from '@microsoft/signalr'
import { createDashboardHub } from './signalRClient'

interface UseDashboardHubOptions {
  onStatsChanged: () => void
  enabled?: boolean
}

export function useDashboardHub({
  onStatsChanged,
  enabled = true,
}: UseDashboardHubOptions): { connected: boolean } {
  const [connected, setConnected] = useState(false)

  const cbRef = useRef(onStatsChanged)
  useEffect(() => {
    cbRef.current = onStatsChanged
  }, [onStatsChanged])

  useEffect(() => {
    if (!enabled) {
      setConnected(false)
      return
    }

    const hub: HubConnection = createDashboardHub()
    let cancelled = false

    hub.on('StatsChanged', () => cbRef.current())

    hub.onreconnecting(() => !cancelled && setConnected(false))
    hub.onreconnected(() => !cancelled && setConnected(true))
    hub.onclose(() => !cancelled && setConnected(false))

    hub
      .start()
      .then(() => {
        if (!cancelled) setConnected(hub.state === HubConnectionState.Connected)
      })
      .catch((err) => {
        // eslint-disable-next-line no-console
        console.warn('[useDashboardHub] start failed:', err)
      })

    return () => {
      cancelled = true
      setConnected(false)
      hub.stop().catch(() => {})
    }
  }, [enabled])

  return { connected }
}
