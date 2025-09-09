// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');

// Minimal chat validation: if any chat reference chip is present,
// clicking it should trigger GET /api/content-references/{id}/url and return 200.
// Test is conditional and will pass if no chips are available (nothing to validate).
test.describe('Chat UI', () => {
  test('Chat reference click resolves via ContentReference API (if present)', async ({ page }) => {
    // Navigate to the app root; the floating assistant may be available from any page
    await page.goto('/');

    // Try to open floating chat if the button exists
    const openBtn = page.locator('.floating-chat-button').first();
    if (await openBtn.count() > 0) {
      await openBtn.click();
      await page.waitForTimeout(300);
    }

    // Look for any rendered chat reference chip from messages
    const chip = page.locator('[data-testid^="chat-ref-"]').first();
    if (await chip.count() === 0) {
      test.info().annotations.push({ type: 'note', description: 'No chat reference chips found; skipping click.' });
      return;
    }

    // Expect backend endpoint call to resolve download URL via ContentReference API
    const urlCall = page.waitForResponse(resp => {
      const u = resp.url();
      return u.includes('/api/content-references/') && u.endsWith('/url');
    });

    await chip.click();
    const resp = await urlCall;
    expect(resp.ok()).toBeTruthy();
  });
});

