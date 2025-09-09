// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');

test.describe('Configuration: Embedding dimension overrides', () => {
  test('dimension override selects are disabled for default ada-002', async ({ page }) => {
    // Navigate to configuration page
    await page.goto('https://localhost:5001/configuration', { waitUntil: 'domcontentloaded' });

    // Scroll to the Content Reference Vector Store section
    await page.getByText('Content Reference Vector Store', { exact: true }).scrollIntoViewIfNeeded();

    // Expect the five dimension override selects are disabled when default model is ada-002
    // App defaults set EmbeddingModelDeploymentName to text-embedding-ada-002 in appsettings.json
    const ids = [
      'dim-override-generated-document',
      'dim-override-generated-section',
      'dim-override-review-item',
      'dim-override-external-file',
      'dim-override-external-link-asset',
    ];

    for (const id of ids) {
      const sel = page.locator(`[data-testid="${id}"]`);
      await expect(sel).toBeVisible();
      await expect(sel).toBeDisabled();
    }
  });
});

