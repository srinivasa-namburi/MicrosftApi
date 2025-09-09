// Copyright (c) Microsoft Corporation. All rights reserved.
const { test, expect, chromium } = require('@playwright/test');

test.describe('Persistent Context Test', () => {
  test('can access main application with persistent Edge profile', async () => {
    console.log('🎯 Testing with persistent Edge profile...');
    
    // Launch Edge with persistent context (maintains login state)
    const context = await chromium.launchPersistentContext('./playwright/edge-profile', {
      headless: false,
      channel: 'msedge',
      executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
      args: ['--disable-blink-features=AutomationControlled'],
      ignoreHTTPSErrors: true
    });
    
    const page = context.pages()[0] || await context.newPage();
    
    // Navigate to the main application
    await page.goto('https://localhost:5001');
    
    // Wait for page to load
    await page.waitForLoadState('networkidle', { timeout: 30000 });
    
    const currentUrl = page.url();
    console.log(`Current URL: ${currentUrl}`);
    
    // Take a screenshot
    await page.screenshot({ 
      path: './playwright/screenshots/persistent-test.png',
      fullPage: true 
    });
    
    // Check if we're on login page OR if we're loading the main app
    const isOnLoginPage = currentUrl.includes('login.microsoftonline.com');
    
    if (isOnLoginPage) {
      console.log('📋 Authentication required - waiting for redirect to main app...');
      
      // Wait for navigation away from login page (up to 2 minutes)
      try {
        await page.waitForFunction(
          () => !window.location.href.includes('login.microsoftonline.com'),
          { timeout: 120000 }
        );
        console.log('✅ Authentication completed - redirected to main application');
        
        // Wait for the app to fully load
        await page.waitForLoadState('networkidle', { timeout: 30000 });
        
        const finalUrl = page.url();
        console.log(`Final URL after auth: ${finalUrl}`);
        
        await page.screenshot({ 
          path: './playwright/screenshots/after-auth.png',
          fullPage: true 
        });
        
        // Test the authenticated application
        const pageTitle = await page.title();
        console.log(`Page title: ${pageTitle}`);
        
        const hasNav = await page.locator('nav, [role="navigation"], .mud-nav-link').count();
        const hasMain = await page.locator('main, [role="main"], .mud-main-content').count();
        
        console.log(`Navigation elements: ${hasNav}`);
        console.log(`Main content elements: ${hasMain}`);
        
        expect(hasNav + hasMain).toBeGreaterThan(0);
        
      } catch (error) {
        console.log('⚠️ Authentication timeout - check manual login in browser');
        throw error;
      }
    } else {
      console.log('✅ Not on login page - checking if main application loaded...');
      
      // Wait for the app to finish loading (it shows "Loading application..." initially)
      console.log('⏳ Waiting for application to finish initializing...');
      
      try {
        // Wait for the loading screen to disappear or main content to appear
        await Promise.race([
          // Wait for loading text to disappear
          page.waitForSelector('text=Loading application...', { state: 'detached', timeout: 30000 }),
          // Or wait for main layout to appear (most reliable)
          page.waitForSelector('[data-testid="main-layout"]', { timeout: 30000 }),
          // Or wait for main application elements to appear
          page.waitForSelector('.mud-appbar', { timeout: 30000 }),
          // Or wait for the site name to appear
          page.waitForSelector('text=Generative AI for Industry Permitting', { timeout: 30000 })
        ]);
        
        console.log('✅ Main application loaded successfully');
        
      } catch (loadError) {
        console.log('⚠️ Application may still be loading, continuing with checks...');
      }
      
      // Take screenshot of loaded app
      await page.screenshot({ 
        path: './playwright/screenshots/main-app-loaded.png',
        fullPage: true 
      });
      
      // Test application content
      const pageTitle = await page.title();
      console.log(`Page title: ${pageTitle}`);
      
      // Look for specific Greenlight application elements
      const mainLayout = await page.locator('[data-testid="main-layout"]').count();
      const appBarCount = await page.locator('.mud-appbar').count();
      const drawerCount = await page.locator('.mud-drawer').count();  
      const siteName = await page.locator('text=Generative AI for Industry Permitting').count();
      const loadingText = await page.locator('text=Loading application...').count();
      
      console.log(`Main layout: ${mainLayout}`);
      console.log(`App bar elements: ${appBarCount}`);
      console.log(`Drawer elements: ${drawerCount}`);
      console.log(`Site name found: ${siteName}`);
      console.log(`Still loading: ${loadingText > 0 ? 'Yes' : 'No'}`);
      
      // Verify we have main application elements (not just loading screen)
      expect(mainLayout + appBarCount + drawerCount + siteName).toBeGreaterThan(0);
      console.log('✅ Authenticated main application verified!');
    }
    
    await context.close();
  });
});