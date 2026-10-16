import { execFileSync } from 'node:child_process'

// Playwright stops the `docker run` client, not always the container itself: remove it explicitly.
export default function globalTeardown() {
  try {
    execFileSync('docker', ['rm', '-f', 'taskhub-e2e'], { stdio: 'ignore' })
  } catch {
    // Already gone, or Docker unavailable: nothing to clean.
  }
}
