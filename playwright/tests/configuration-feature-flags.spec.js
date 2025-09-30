const { test, expect } = require('@playwright/test');

test.describe('Configuration > Feature Flags', () => {
  test('toggling a flag marks section dirty and enables save', async ({ page }) => {
    // Navigate to Configuration page (assumes auth session is already established)
    await page.goto('/admin/configuration');

    // Wait for the Feature Flags section to render
    await expect(page.getByText('Feature Flags')).toBeVisible();

    // Locate the Feature Flags section's Save button
    const saveButton = page.getByRole('button', { name: 'Save' }).first();
    await expect(saveButton).toBeDisabled();

    // Toggle one of the feature flags by its label
    const reviewsSwitch = page.getByLabel('Enable Reviews');
    await expect(reviewsSwitch).toBeVisible();
    await reviewsSwitch.click();

    // Save button should now be enabled for the section
    await expect(saveButton).toBeEnabled();
  });
});

