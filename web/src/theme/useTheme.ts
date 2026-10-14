import { useEffect, useState } from 'react'

export type ThemePreference = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'taskhub.theme'
const ORDER: ThemePreference[] = ['system', 'light', 'dark']

function readPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return stored === 'light' || stored === 'dark' ? stored : 'system'
  } catch {
    return 'system'
  }
}

function systemPrefersDark(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: dark)').matches
}

/** Light/dark theme with a "follow the system" default, remembered per browser. */
export function useTheme() {
  const [preference, setPreference] = useState<ThemePreference>(readPreference)

  useEffect(() => {
    const apply = () => {
      const dark = preference === 'dark' || (preference === 'system' && systemPrefersDark())
      document.documentElement.classList.toggle('dark', dark)
    }
    apply()

    try {
      if (preference === 'system') localStorage.removeItem(STORAGE_KEY)
      else localStorage.setItem(STORAGE_KEY, preference)
    } catch {
      // Storage unavailable (private mode): the choice simply is not remembered.
    }

    if (preference !== 'system' || typeof window.matchMedia !== 'function') return
    const media = window.matchMedia('(prefers-color-scheme: dark)')
    media.addEventListener('change', apply)
    return () => media.removeEventListener('change', apply)
  }, [preference])

  const cycle = () => setPreference((current) => ORDER[(ORDER.indexOf(current) + 1) % ORDER.length])

  return { preference, cycle }
}
