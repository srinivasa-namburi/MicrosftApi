// Copyright (c) Microsoft Corporation. All rights reserved.
const { chromium } = require('playwright');
const { test, expect } = require('@playwright/test');
const path = require('path');

test.describe('MudBlazor Navigation Test', () => {
  test('navigate through MudNavGroup to Configuration', async () => {
    // Use persistent context with Edge profile
    const userDataDir = './playwright/edge-profile';
    const browser = await chromium.launchPersistentContext(userDataDir, {
      headless: false,
      channel: 'msedge',
      executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
      ignoreHTTPSErrors: true,
      args: ['--disable-blink-features=AutomationControlled']
    });

    const page = await browser.newPage();
    
    try {
      console.log('🎯 Navigating to Blazor WebAssembly application...');
      await page.goto('https://localhost:5001', { 
        waitUntil: 'load',
        timeout: 30000
      });

      const currentUrl = page.url();
      console.log(`📍 Current URL: ${currentUrl}`);

      // If we're on login page, wait for manual authentication
      if (currentUrl.includes('login.microsoftonline.com')) {
        console.log('🔑 Please complete authentication manually...');
        await page.waitForURL((url) => !url.includes('login.microsoftonline.com'), {
          timeout: 60000
        });
        console.log('✅ Authentication completed');
      }

      // Wait for Blazor WebAssembly to fully load
      console.log('⏳ Waiting for Blazor WebAssembly to initialize...');
      await page.waitForSelector('nav, .mud-drawer, .mud-main-content, main', { timeout: 15000 });
      
      console.log('✅ Blazor WebAssembly appears to be loaded');
      
      // Take initial screenshot
      await page.screenshot({ 
        path: './playwright/screenshots/mudblazor-1-initial-home.png',
        fullPage: true 
      });
      console.log('📸 Initial home page screenshot taken');

      // Look for Administration nav group by test id
      console.log('🔍 Looking for Administration nav group...');
      const adminGroup = page.locator('[data-testid="admin-nav-group"]');
      await adminGroup.waitFor({ state: 'visible', timeout: 10000 });
      console.log('✅ Found Administration nav group');

      // Expand the Administration group by clicking on it
      console.log('📂 Expanding Administration group...');
      await adminGroup.click();
      await page.waitForTimeout(500); // Wait for animation
      
      // Take screenshot after expanding group
      await page.screenshot({ 
        path: './playwright/screenshots/mudblazor-2-admin-expanded.png',
        fullPage: true 
      });
      console.log('📸 Administration group expanded screenshot taken');

      // Now look for Configuration link
      console.log('🔍 Looking for Configuration link...');
      const configLink = page.locator('[data-testid="configuration-nav-link"]');
      await configLink.waitFor({ state: 'visible', timeout: 10000 });
      console.log('✅ Found Configuration link');
      
      // Click on Configuration link
      console.log('🔗 Clicking on Configuration...');
      await configLink.click();
      
      // Wait for navigation to complete
      await page.waitForURL('**/configuration', { timeout: 10000 });
      await page.waitForTimeout(1000); // Wait for content to render
      
      // Verify we're on the configuration page
      const configUrl = page.url();
      console.log(`📍 Configuration page URL: ${configUrl}`);
      expect(configUrl).toContain('/configuration');
      
      // Take screenshot of configuration page
      await page.screenshot({ 
        path: './playwright/screenshots/mudblazor-3-configuration-page.png',
        fullPage: true 
      });
      console.log('📸 Configuration page screenshot taken');
      
      // Navigate back to Home using nav link
      console.log('🏠 Navigating back to Home...');
      const homeLink = page.locator('.mud-nav-link').filter({ hasText: 'Home' }).first();
      await homeLink.waitFor({ state: 'visible' });
      await homeLink.click();
      
      // Wait for navigation back to home
      await page.waitForURL('https://localhost:5001/', { timeout: 10000 });
      
      // Verify we're back on the home page
      const homeUrl = page.url();
      console.log(`📍 Back to home URL: ${homeUrl}`);
      const isAtHome = homeUrl === 'https://localhost:5001/' || homeUrl.endsWith(':5001/');
      expect(isAtHome).toBe(true);
      
      // Take final screenshot
      await page.screenshot({ 
        path: './playwright/screenshots/mudblazor-4-back-to-home.png',
        fullPage: true 
      });
      console.log('📸 Final home page screenshot taken');
      
      console.log('✅ MudBlazor navigation test completed successfully!');
      console.log('📁 Screenshots saved in: playwright/screenshots/');
      
    } catch (error) {
      console.error('❌ Test failed:', error.message);
      await page.screenshot({ 
        path: './playwright/screenshots/mudblazor-error-state.png',
        fullPage: true 
      });
      throw error;
    } finally {
      await browser.close();
    }
  });
});