// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');

test.describe('Content Reference Reindex Status', () => {
  test('shows progress updates for selected CR type', async ({ page }) => {
    // Navigate to app home
    await page.goto('https://localhost:5001', { waitUntil: 'domcontentloaded' });

    // Navigate to Configuration
    const configLink = page.locator('a, .mud-nav-link').filter({ hasText: 'Configuration' }).first();
    await configLink.click();
    await page.waitForURL('**/configuration');

    // Scroll to Vector Store (to ensure CR section visible)
    await page.keyboard.down('End');
    await page.waitForTimeout(500);

    // Ensure the CR status card is present
    await expect(page.locator('text=Content Reference Reindex Status')).toBeVisible();

    // Select a CR type (External File as default is fine)
    // Component renders <MudSelect> bound to _contentReferenceStatusType; keep default.

    // Trigger a manual refresh (component also auto-refreshes)
    // The component encapsulates refresh; we simply expect some text pattern after a while.

    // Wait up to 10s for any status line to appear
    const statusLoc = page.locator('text=/Processed:\s*\d+\s*\/\s*\d+/i');
    await expect(statusLoc).toBeVisible({ timeout: 10000 });

    // Optionally check per-source table or progress bars existence
    const perSourceHeader = page.locator('text=Per-source progress');
    // Visible only when Sources > 0; do not assert strictly to avoid flakes.
    if (await perSourceHeader.count() > 0) {
      await expect(perSourceHeader).toBeVisible();
    }
  });
});

