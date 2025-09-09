// Copyright (c) Microsoft Corporation. All rights reserved.
const { chromium, expect } = require('@playwright/test');

// Standalone test that doesn't use any Playwright config dependencies
async function runStandaloneTest() {
  console.log('🎯 Running completely standalone Playwright test...');
  
  let context;
  let page;
  
  try {
    // Launch Edge with persistent context (maintains login state)
    console.log('🚀 Launching Microsoft Edge with persistent profile...');
    context = await chromium.launchPersistentContext('./playwright/edge-profile', {
      headless: false,
      channel: 'msedge',
      executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
      args: ['--disable-blink-features=AutomationControlled'],
      ignoreHTTPSErrors: true,
      timeout: 30000
    });
    
    page = context.pages()[0] || await context.newPage();
    
    console.log('🌐 Navigating to Greenlight application...');
    await page.goto('https://localhost:5001', { 
      timeout: 60000,
      waitUntil: 'domcontentloaded' 
    });
    
    console.log('📄 Page loaded, waiting for network to settle...');
    // Wait for page to load with more patience
    try {
      await page.waitForLoadState('networkidle', { timeout: 60000 });
      console.log('✅ Network settled');
    } catch (networkError) {
      console.log('⚠️ Network still active, but continuing...');
    }
    
    const currentUrl = page.url();
    console.log(`📍 Current URL: ${currentUrl}`);
    
    // Take initial screenshot
    await page.screenshot({ 
      path: './playwright/screenshots/standalone-initial.png',
      fullPage: true 
    });
    
    // Check if we're on login page or main app
    const isOnLoginPage = currentUrl.includes('login.microsoftonline.com');
    
    if (isOnLoginPage) {
      console.log('🔐 Authentication required - waiting for manual login...');
      
      // Wait for navigation away from login (up to 3 minutes)
      await page.waitForFunction(
        () => !window.location.href.includes('login.microsoftonline.com'),
        { timeout: 180000 }
      );
      
      console.log('✅ Authentication completed - redirected to main application');
      await page.waitForLoadState('networkidle', { timeout: 30000 });
    } else {
      console.log('✅ Already on main application - persistent auth working!');
    }
    
    // Wait for application to fully load
    console.log('⏳ Waiting for Greenlight application to initialize...');
    
    try {
      await Promise.race([
        page.waitForSelector('[data-testid="main-layout"]', { timeout: 45000 }),
        page.waitForSelector('.mud-appbar', { timeout: 45000 }),
        page.waitForSelector('text=Generative AI for Industry Permitting', { timeout: 45000 })
      ]);
      console.log('✅ Application loaded successfully');
    } catch (loadError) {
      console.log('⚠️ Application still loading, continuing with verification...');
    }
    
    // Final screenshot
    await page.screenshot({ 
      path: './playwright/screenshots/standalone-final.png',
      fullPage: true 
    });
    
    // Verify application content
    const pageTitle = await page.title();
    console.log(`📄 Page title: ${pageTitle}`);
    
    const mainLayout = await page.locator('[data-testid="main-layout"]').count();
    const appBar = await page.locator('.mud-appbar').count();
    const drawer = await page.locator('.mud-drawer').count();
    const siteName = await page.locator('text=Generative AI for Industry Permitting').count();
    const isLoading = await page.locator('text=Loading application...').count();
    
    console.log(`🎛️ Main layout elements: ${mainLayout}`);
    console.log(`🎛️ App bar elements: ${appBar}`);
    console.log(`🎛️ Drawer elements: ${drawer}`);
    console.log(`🏷️ Site name found: ${siteName}`);
    console.log(`⏳ Still loading: ${isLoading > 0 ? 'Yes' : 'No'}`);
    
    const totalElements = mainLayout + appBar + drawer + siteName;
    console.log(`📊 Total app elements found: ${totalElements}`);
    
    if (totalElements > 0) {
      console.log('🎉 SUCCESS: Authenticated Greenlight application verified!');
      console.log('🎭 Playwright can now control the authenticated application');
      return true;
    } else {
      console.log('❌ FAIL: Could not verify main application elements');
      return false;
    }
    
  } catch (error) {
    console.error('❌ Test failed:', error.message);
    return false;
  } finally {
    if (context) {
      console.log('🔄 Closing browser context...');
      await context.close();
    }
  }
}

// Run the test
runStandaloneTest()
  .then(success => {
    console.log(`\n🏁 Test completed: ${success ? 'PASSED' : 'FAILED'}`);
    process.exit(success ? 0 : 1);
  })
  .catch(error => {
    console.error('💥 Unexpected error:', error);
    process.exit(1);
  });