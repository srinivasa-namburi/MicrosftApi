# Playwright Testing for Greenlight

This directory contains the Playwright automation setup for the Microsoft Greenlight solution.

## Quick Start

### 1. Install Dependencies
```bash
# Local development (Windows with Edge support)
npm run install-deps

# CI/Linux (Chromium only)
npm run install-deps-ci
```

### 2. Start the Solution
```bash
# Start solution normally
npm start

# Start with hot reload (development)
npm run start:watch

# Check if solution is running
npm run status
```

### 3. Run Tests
```bash
# Run all tests
npm test

# Run with visible browser (local development)
npm run test:headed

# Run with specific browser
npm run test:msedge      # Microsoft Edge (Windows)
npm run test:chromium    # Chromium (cross-platform)

# Debug tests interactively
npm run test:debug

# Open application in default Edge profile (natural Edge behavior)
npm run browser
```

## Directory Structure

```
playwright/
├── README.md                 # This file
├── playwright.config.js     # Main configuration
├── tests/                   # Test files
│   ├── auth.setup.js        # Authentication setup
│   └── greenlight.spec.js   # Main application tests
├── auth/                    # Authentication state (gitignored)
├── screenshots/             # Test screenshots (gitignored)
└── test-results/           # Test results and artifacts (gitignored)
```

## Configuration

### Browser Support
- **Local Development (Windows)**: Microsoft Edge preferred, Chromium fallback
- **CI/Linux**: Chromium only for headless testing
- **Platform Detection**: Automatic browser selection based on environment

### Authentication
- Uses Entra ID (Azure AD) authentication
- Captures authentication state from existing browser sessions
- Reuses saved authentication state across test runs
- Handles both token-based (Aspire dashboard) and interactive authentication

### Aspire Integration
- Automatically starts/stops the Greenlight solution
- Waits for all services to be healthy before running tests
- Monitors both Aspire dashboard (port 17209) and main app (port 7243)
- 5-minute timeout for full solution startup

## Solution Lifecycle Management

The `scripts/manage-solution.ps1` provides comprehensive solution management:

```bash
# Solution management
npm start           # Smart start (checks if running)
npm run start:watch # Start with hot reload
npm stop            # Stop all processes
npm restart         # Stop and start
npm run status      # Show detailed status
```

### What It Does
- ✅ Detects existing processes using Aspire ports
- ✅ Kills conflicting processes cleanly
- ✅ Verifies Docker is running (required for Aspire)
- ✅ Tests HTTP connectivity to endpoints
- ✅ Provides detailed status information

## Test Development

### Writing Tests
```javascript
// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');

test.describe('My Feature', () => {
  test.beforeEach(async ({ page }) => {
    // Authentication state is automatically loaded
    await page.goto('/my-feature');
  });

  test('can do something', async ({ page }) => {
    // Test logic here
    await page.screenshot({ 
      path: 'playwright/screenshots/my-feature.png',
      fullPage: true 
    });
  });
});
```

### Page Object Pattern
Consider using page objects for complex interactions:

```javascript
class DashboardPage {
  constructor(page) {
    this.page = page;
    this.nav = page.locator('[role="navigation"]');
    this.userMenu = page.locator('[data-testid="user-menu"]');
  }
  
  async goto() {
    await this.page.goto('/dashboard');
    await this.nav.waitFor();
  }
}
```

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Setup Playwright
  run: npm run install-deps-ci

- name: Run tests
  run: npm test
  env:
    CI: true
```

### Azure DevOps Example
```yaml
- script: npm run install-deps-ci
  displayName: 'Install Playwright'

- script: npm test
  displayName: 'Run Playwright tests'
  env:
    CI: true
```

## Troubleshooting

### Common Issues

**Solution won't start:**
```bash
npm run status    # Check what's running
npm stop          # Kill everything
npm restart       # Fresh start
```

**Authentication fails:**
```bash
# Clear auth state and restart tests - authentication happens automatically
rm playwright/auth/*.json
npm run test:headed
```

**Tests timeout:**
- Increase timeout in `playwright.config.js`
- Check that all Aspire services are healthy
- Verify Docker is running for dependencies

**Browser not found:**
```bash
# Force reinstall browsers
npm run install-deps -- -Force
```

### Debug Mode
```bash
# Interactive debugging
npm run test:debug

# Run specific test file
cd playwright && npx playwright test tests/my-test.spec.js --debug
```

### Trace Viewer
```bash
# Show test traces after failure
npm run show-report
```

## Platform Support

| Platform | Local Dev | CI | Browser | Notes |
|----------|-----------|----|---------|----- |
| Windows | ✅ | ✅ | Edge → Chromium | Uses existing Edge if available |
| Linux | ✅ | ✅ | Chromium | Includes system dependencies |
| macOS | ✅ | ✅ | Chromium | Standard Playwright install |

## Security Notes

- Authentication state files are excluded from source control
- Screenshots may contain sensitive information
- Test results are not committed to repository
- Always review authentication setup for production use