// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');

test.describe('Reviews UI', () => {
  test('Recent Reviews renders, and download resolves via ContentReference API if available', async ({ page }) => {
    await page.goto('/reviews');

    // Title present
    await expect(page.locator('[data-testid="reviews-title"]')).toHaveText('Review Definitions');

    // Recent Reviews section visible
    const recentSection = page.locator('[data-testid="recent-reviews"]');
    await expect(page.locator('[data-testid="recent-reviews-title"]')).toBeVisible();

    // If no recent list is present, pass (info alert may be shown)
    const hasList = await recentSection.count();
    if (hasList === 0) {
      test.info().annotations.push({ type: 'note', description: 'No recent reviews; skipped download click.' });
      return;
    }

    // Try to click first available download button, if any
    const downloadBtn = page.locator('[data-testid^="download-reference-"]').first();
    if (await downloadBtn.count() > 0) {
      // Expect backend URL resolution call to succeed (200)
      const urlCall = page.waitForResponse(resp => {
        const u = resp.url();
        return u.includes('/api/content-references/') && u.endsWith('/url');
      });
      await downloadBtn.click();
      const resp = await urlCall;
      expect(resp.ok()).toBeTruthy();
    } else {
      test.info().annotations.push({ type: 'note', description: 'No download buttons found in recent reviews.' });
    }
  });
});

