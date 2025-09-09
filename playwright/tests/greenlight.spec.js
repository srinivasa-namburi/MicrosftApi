// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');
const { handleAuthentication } = require('./auth-helper');

test.describe('Greenlight Application', () => {
  test.beforeEach(async ({ page }) => {
    // Handle authentication if needed
    await handleAuthentication(page);
  });

  test('can access the dashboard', async ({ page }) => {
    console.log('🎯 Testing dashboard access...');
    
    // Wait for the page to load
    await page.waitForLoadState('networkidle');
    
    // Take a screenshot
    await page.screenshot({ 
      path: './playwright/screenshots/dashboard.png',
      fullPage: true 
    });
    
    // Check that we're on the main application (not login page)
    const currentUrl = page.url();
    console.log(`Current URL: ${currentUrl}`);
    
    // Should not be on Microsoft login page
    expect(currentUrl).not.toContain('login.microsoftonline.com');
    
    // Check for basic page structure (adjust selectors based on actual app)
    const pageTitle = await page.title();
    console.log(`Page title: ${pageTitle}`);
    
    // Verify we have some navigation or content
    const hasNavigation = await page.locator('nav, [role="navigation"], .mud-nav-link').count();
    const hasContent = await page.locator('main, .mud-main-content, [role="main"]').count();
    
    console.log(`Navigation elements found: ${hasNavigation}`);
    console.log(`Main content areas found: ${hasContent}`);
    
    // At least one of these should be present in the authenticated app
    expect(hasNavigation + hasContent).toBeGreaterThan(0);
  });

  test('can interact with UI elements', async ({ page }) => {
    console.log('🎭 Testing UI interaction...');
    
    await page.waitForLoadState('networkidle');
    
    // Find clickable elements
    const buttons = await page.locator('button').count();
    const links = await page.locator('a').count();
    const inputs = await page.locator('input').count();
    
    console.log(`Found ${buttons} buttons, ${links} links, ${inputs} inputs`);
    
    // Take a screenshot showing the UI
    await page.screenshot({ 
      path: './playwright/screenshots/ui-elements.png',
      fullPage: true 
    });
    
    // Verify we have interactive elements
    expect(buttons + links).toBeGreaterThan(0);
  });
});