import { useEffect, useRef } from 'react'

/**
 * setInterval but with a stable callback reference.
 * Pass `null` as the delay to pause.
 */
export function useInterval(callback: () => void, delay: number | null) {
  const savedCallback = useRef(callback)

  useEffect(() => {
    savedCallback.current = callback
  }, [callback])

  useEffect(() => {
    if (delay === null) return
    const tick = () => savedCallback.current()
    const id = setInterval(tick, delay)
    return () => clearInterval(id)
  }, [delay])
}
