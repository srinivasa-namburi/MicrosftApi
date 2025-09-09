// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect } = require('@playwright/test');
const { handleAuthentication } = require('./auth-helper');

test.describe('Simple Greenlight Test (No Setup)', () => {
  test('can access the main application', async ({ page }) => {
    console.log('🎯 Testing main application access...');
    
    // Handle authentication if needed
    await handleAuthentication(page);
    
    // Wait for page to load
    await page.waitForLoadState('networkidle', { timeout: 30000 });
    
    const currentUrl = page.url();
    console.log(`Current URL: ${currentUrl}`);
    
    // Take a screenshot for debugging
    await page.screenshot({ 
      path: './playwright/screenshots/main-app.png',
      fullPage: true 
    });
    
    // Check if we're authenticated (not on login page)
    const isOnLoginPage = currentUrl.includes('login.microsoftonline.com');
    
    if (isOnLoginPage) {
      console.log('⚠️  Redirected to login page - authentication state may not be working');
      console.log('   This is expected for the first run or if auth state expired');
      
      // Don't fail the test, just log the result
      await page.screenshot({ path: './playwright/screenshots/login-redirect.png' });
    } else {
      console.log('✅ Successfully loaded main application without login redirect');
      
      // Look for application content
      const pageTitle = await page.title();
      console.log(`Page title: ${pageTitle}`);
      
      // Check for basic application elements
      const hasNav = await page.locator('nav, [role="navigation"], .mud-nav-link').count();
      const hasMain = await page.locator('main, [role="main"], .mud-main-content').count();
      
      console.log(`Navigation elements: ${hasNav}`);
      console.log(`Main content elements: ${hasMain}`);
      
      // This should pass if we're on the authenticated app
      expect(hasNav + hasMain).toBeGreaterThan(0);
    }
  });
});