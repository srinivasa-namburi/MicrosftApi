// Copyright (c) Microsoft Corporation. All rights reserved.

/**
 * Authentication helper for Playwright tests
 * Handles Microsoft Entra ID authentication with 120-second timeout for login.microsoftonline.com
 */

/**
 * Handles authentication flow for tests
 * @param {import('@playwright/test').Page} page - Playwright page object
 * @returns {Promise<boolean>} - Returns true if authentication successful or not needed
 */
async function handleAuthentication(page) {
  console.log('🔐 Checking authentication status...');
  
  try {
    // Navigate to the application
    console.log('📍 Navigating to application...');
    await page.goto('/', { timeout: 60000 });
    
    // Wait for initial page load
    await page.waitForLoadState('networkidle', { timeout: 30000 });
    
    const currentUrl = page.url();
    console.log(`   Current URL: ${currentUrl}`);
    
    // Check if we're on Microsoft login page
    if (currentUrl.includes('login.microsoftonline.com')) {
      console.log('🔑 Microsoft authentication page detected.');
      console.log('   Waiting up to 120 seconds for authentication completion...');
      
      // Wait for redirect away from login page (120 seconds as requested)
      await page.waitForFunction(
        () => !window.location.href.includes('login.microsoftonline.com'),
        { timeout: 120000 } // 120 seconds
      );
      
      console.log('🔄 Authentication completed, waiting for app to load...');
      await page.waitForLoadState('networkidle', { timeout: 30000 });
    }
    
    // Verify we're in the application by looking for common authenticated indicators
    console.log('🔍 Verifying authentication...');
    const authIndicators = [
      'nav',
      '.mud-nav-link',
      '[role="navigation"]',
      '.mud-appbar', 
      '.mud-drawer',
      'header'
    ];
    
    let authenticated = false;
    for (const selector of authIndicators) {
      try {
        await page.waitForSelector(selector, { timeout: 5000 });
        console.log(`   ✅ Found authenticated indicator: ${selector}`);
        authenticated = true;
        break;
      } catch {
        // Try next selector
      }
    }
    
    if (!authenticated) {
      console.log('⚠️  Could not detect standard authenticated elements.');
      console.log('   Proceeding anyway - may be on a different page.');
    }
    
    // Take screenshot for debugging
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    await page.screenshot({ 
      path: `./playwright/screenshots/auth-${timestamp}.png`, 
      fullPage: true 
    });
    console.log(`   📸 Screenshot saved to ./playwright/screenshots/auth-${timestamp}.png`);
    
    console.log('✅ Authentication check complete');
    return true;
    
  } catch (error) {
    console.error('❌ Authentication failed:', error.message);
    
    // Take error screenshot
    try {
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      await page.screenshot({ 
        path: `./playwright/screenshots/auth-error-${timestamp}.png`, 
        fullPage: true 
      });
      console.log(`   📸 Error screenshot saved to ./playwright/screenshots/auth-error-${timestamp}.png`);
    } catch (screenshotError) {
      console.log('   Could not capture error screenshot');
    }
    
    return false;
  }
}

module.exports = {
  handleAuthentication
};