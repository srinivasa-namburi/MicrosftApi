// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');

// Validate that opening the AI Assistant triggers the new
// /api/content-references/assistant-list endpoint to populate the selector.
// If the floating assistant button is not present, skip gracefully.
test.describe('AI Assistant reference list', () => {
  test('Loads assistant list endpoint when opened (if present)', async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });

    const openBtn = page.locator('.floating-chat-button').first();
    if (await openBtn.count() === 0) {
      test.info().annotations.push({ type: 'note', description: 'No floating assistant button; skipping assistant-list validation.' });
      return;
    }

    const assistantListCall = page.waitForResponse(resp => resp.url().includes('/api/content-references/assistant-list'));

    await openBtn.click();

    const resp = await assistantListCall;
    expect(resp.ok()).toBeTruthy();
  });
});

