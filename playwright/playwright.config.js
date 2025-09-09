// Copyright (c) Microsoft Corporation. All rights reserved.
const { defineConfig, devices } = require('@playwright/test');

/**
 * @see https://playwright.dev/docs/test-configuration
 */
module.exports = defineConfig({
  testDir: './tests',
  fullyParallel: false, // Sequential for Aspire startup
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : 1, // Single worker for better reliability
  reporter: [
    ['list'],
    ['html', { outputFolder: './playwright/test-results/html-report' }],
    ['json', { outputFile: './playwright/test-results/results.json' }]
  ],
  outputDir: './playwright/test-results/artifacts',
  timeout: 30 * 1000, // 30 seconds per test
  expect: {
    timeout: 10 * 1000, // 10 seconds for assertions
  },
  use: {
    baseURL: 'https://localhost:5001', // web-docgen frontend from Aspire
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },

  projects: [
    // Microsoft Edge with natural profile behavior (primary testing)
    {
      name: 'msedge',
      use: { 
        ...devices['Desktop Chrome'],
        channel: 'msedge',
        launchOptions: {
          executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe'
        }
      }
    },

    // CI/fallback with Chromium (for headless CI environments)
    {
      name: 'chromium',
      use: { 
        ...devices['Desktop Chrome'],
        // Load saved authentication state if available
        storageState: './playwright/auth/user.json'
      }
    }
  ],

  // Configure Aspire web server for automated testing
  webServer: {
    command: 'cd src && dotnet run --project Microsoft.Greenlight.AppHost',
    url: 'https://localhost:5001', // Wait for main web-docgen app to be ready
    timeout: 300 * 1000, // 5 minutes for Aspire + all services to start
    reuseExistingServer: !process.env.CI,
    ignoreHTTPSErrors: true,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_ENVIRONMENT: 'Development'
    }
  },
});