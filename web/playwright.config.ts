import { defineConfig, devices } from '@playwright/test'

const appPort = 8080
const mockPort = 4010
const image = process.env.E2E_IMAGE ?? 'taskhub:e2e'

// The tests run against the real Docker image (build it first: npm run e2e:image), which is what
// catches problems the unit tests cannot see, such as static assets answered with index.html.
// The container uses the host network to reach the fake Azure DevOps server below.
const containerArgs = [
  'docker run --rm --name taskhub-e2e --network host',
  `-e ASPNETCORE_HTTP_PORTS=${appPort}`,
  '-e ConnectionStrings__TaskHub="Data Source=/tmp/e2e.db"',
  `-e Sources__AzureDevOps__BaseUrl=http://127.0.0.1:${mockPort}/`,
  '-e Sources__AzureDevOps__Organization=e2e',
  '-e Sources__AzureDevOps__Pat=e2e-not-a-secret',
  '-e Sync__IntervalMinutes=1440',
  image,
].join(' ')

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  globalTeardown: './e2e/global-teardown.ts',
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://127.0.0.1:${appPort}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'node e2e/mock-azure-devops.mjs',
      url: `http://127.0.0.1:${mockPort}/__health`,
      reuseExistingServer: !process.env.CI,
    },
    {
      command: containerArgs,
      url: `http://127.0.0.1:${appPort}/api/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
})
