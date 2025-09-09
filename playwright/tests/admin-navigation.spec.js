// Copyright (c) Microsoft Corporation. All rights reserved.
const { chromium } = require('playwright');
const { test, expect } = require('@playwright/test');
const path = require('path');

test.describe('Administration Navigation Test', () => {
  test('navigate to Configuration and back to Home', async () => {
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
        waitUntil: 'load',  // Just wait for basic page load, not networkidle
        timeout: 30000      // Reduced timeout - app should load quickly
      });

      const currentUrl = page.url();
      console.log(`📍 Current URL: ${currentUrl}`);

      // If we're on login page, wait for manual authentication
      if (currentUrl.includes('login.microsoftonline.com')) {
        console.log('🔑 Please complete authentication manually...');
        await page.waitForURL((url) => !url.includes('login.microsoftonline.com'), {
          timeout: 60000  // Reduced from 120s
        });
        console.log('✅ Authentication completed');
      }

      // Wait for Blazor WebAssembly to fully load by looking for specific content
      console.log('⏳ Waiting for Blazor WebAssembly to initialize...');
      
      // Wait for basic page elements to appear
      await page.waitForSelector('body', { timeout: 10000 });
      
      // Wait for Blazor content - look for the main content area or navigation
      await page.waitForSelector('nav, .mud-drawer, .mud-main-content, main', { timeout: 15000 });
      
      // Look for navigation elements - could be in mini mode
      const navExists = await page.locator('nav, .mud-drawer, .custom-nav-menu').count();
      console.log(`📊 Navigation elements found: ${navExists}`);
      
      console.log('✅ Blazor WebAssembly appears to be loaded');
      
      // Take initial screenshot
      await page.screenshot({ 
        path: './playwright/screenshots/1-initial-home.png',
        fullPage: true 
      });
      console.log('📸 Initial home page screenshot taken');

      // Check if navigation is in mini mode and try to expand it
      console.log('🔍 Checking navigation state...');
      
      const isMiniMode = await page.locator('.mud-drawer--mini').count() > 0;
      console.log(`📱 Navigation mini mode: ${isMiniMode}`);
      
      // If in mini mode, try to expand by clicking hamburger menu or drawer toggle
      if (isMiniMode) {
        console.log('📂 Attempting to expand navigation from mini mode...');
        
        // Look for hamburger menu or drawer toggle button
        const hamburgerBtn = page.locator('button[aria-label*="menu"], .mud-button:has(.fas.fa-bars), .mud-icon-button:has(.fas.fa-bars)').first();
        const hamburgerExists = await hamburgerBtn.count() > 0;
        
        if (hamburgerExists) {
          await hamburgerBtn.click();
          await page.waitForTimeout(500); // Wait for animation
          console.log('🔄 Clicked hamburger menu to expand navigation');
        } else {
          console.log('⚠️ No hamburger menu found, navigation may be permanently in mini mode');
        }
      }
      
      // Take screenshot of current state
      await page.screenshot({ 
        path: './playwright/screenshots/2-navigation-state.png',
        fullPage: true 
      });
      console.log('📸 Navigation state screenshot taken');
      
      // Look for Configuration link - try different approaches based on navigation state
      console.log('🔍 Looking for Configuration link...');
      
      // Try to find Configuration link by text first
      let configurationLink = page.locator('a, .mud-nav-link').filter({ hasText: 'Configuration' });
      let configLinkExists = await configurationLink.count() > 0;
      
      if (!configLinkExists) {
        // If text not found, look for settings/cog icon that might be the Configuration link
        console.log('🔧 Looking for Configuration by icon (gear/cog)...');
        configurationLink = page.locator('a:has(.fa-sliders-h), .mud-nav-link:has(.fa-sliders-h)').first();
        configLinkExists = await configurationLink.count() > 0;
      }
      
      if (!configLinkExists) {
        // Last resort - look for any link with href containing "configuration"
        console.log('🔗 Looking for Configuration by href...');
        configurationLink = page.locator('a[href*="configuration"]').first();
        configLinkExists = await configurationLink.count() > 0;
      }
      
      if (configLinkExists) {
        console.log('✅ Found Configuration link');
        
        // Click on Configuration link
        console.log('🔗 Clicking on Configuration...');
        await configurationLink.click();
        
        // Wait for Blazor navigation to complete (just wait for URL change)
        await page.waitForURL('**/configuration');
        await page.waitForTimeout(500); // Brief wait for content to render
        
        // Verify we're on the configuration page
        const configUrl = page.url();
        console.log(`📍 Configuration page URL: ${configUrl}`);
        expect(configUrl).toContain('/configuration');
        
        // Take screenshot of configuration page
        await page.screenshot({ 
          path: './playwright/screenshots/3-configuration-page.png',
          fullPage: true 
        });
        console.log('📸 Configuration page screenshot taken');
        
        // Look for configuration page elements
        const configPageTitle = await page.locator('h1, h2, h3, .mud-typography-h1, .mud-typography-h2, .mud-typography-h3').filter({ hasText: /configuration/i }).count();
        console.log(`📄 Configuration page title elements found: ${configPageTitle}`);
        
        // Navigate back to Home using MudBlazor selector
        console.log('🏠 Navigating back to Home...');
        const homeLink = page.locator('.mud-nav-link').filter({ hasText: 'Home' }).first();
        
        // Ensure home link is clickable
        await homeLink.waitFor({ state: 'visible' });
        await homeLink.click();
        
        // Wait for Blazor navigation to complete (URL change)
        await page.waitForURL('https://localhost:5001/');
        
        // Brief wait for Blazor to re-render home page content
        await page.waitForTimeout(300);
        
        // Verify we're back on the home page by checking URL or content
        const homeUrl = page.url();
        console.log(`📍 Back to home URL: ${homeUrl}`);
        
        // For Blazor apps, the URL might be exactly the base URL
        const isAtHome = homeUrl === 'https://localhost:5001/' || homeUrl.endsWith(':5001/');
        expect(isAtHome).toBe(true);
        
        // Take final screenshot
        await page.screenshot({ 
          path: './playwright/screenshots/4-back-to-home.png',
          fullPage: true 
        });
        console.log('📸 Final home page screenshot taken');
        
        console.log('✅ Navigation test completed successfully!');
        console.log('📁 Screenshots saved in: playwright/screenshots/');
      } else {
        console.log('❌ Could not find Configuration link');
        throw new Error('Configuration link not found');
      }
      
    } catch (error) {
      console.error('❌ Test failed:', error.message);
      await page.screenshot({ 
        path: './playwright/screenshots/error-state.png',
        fullPage: true 
      });
      throw error;
    } finally {
      await browser.close();
    }
  });
});