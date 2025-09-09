// Copyright (c) Microsoft Corporation. All rights reserved.
const { chromium, _electron: electron } = require('playwright');
const { test, expect } = require('@playwright/test');
const path = require('path');

test.describe('Navigation Menu Test', () => {
  test('check MudBlazor navigation implementation', async () => {
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
      console.log('🎯 Navigating to application...');
      await page.goto('https://localhost:5001', { 
        waitUntil: 'networkidle',
        timeout: 60000 
      });

      const currentUrl = page.url();
      console.log(`📍 Current URL: ${currentUrl}`);

      // If we're on login page, wait for manual authentication
      if (currentUrl.includes('login.microsoftonline.com')) {
        console.log('🔑 Please complete authentication manually...');
        await page.waitForURL((url) => !url.includes('login.microsoftonline.com'), {
          timeout: 120000
        });
        console.log('✅ Authentication completed');
      }

      // Wait for the page to stabilize
      await page.waitForLoadState('networkidle');
      
      // Take screenshot of the full page
      await page.screenshot({ 
        path: './playwright/screenshots/nav-menu-mudblazor.png',
        fullPage: true 
      });

      // Check for MudBlazor navigation components
      console.log('🔍 Checking for MudBlazor navigation components...');
      
      const mudNavMenu = await page.locator('.mud-nav-menu').count();
      const mudNavLink = await page.locator('.mud-nav-link').count();
      const mudNavGroup = await page.locator('.mud-nav-group').count();
      
      console.log(`  MudNavMenu elements: ${mudNavMenu}`);
      console.log(`  MudNavLink elements: ${mudNavLink}`);
      console.log(`  MudNavGroup elements: ${mudNavGroup}`);
      
      // Check if icons are visible
      const icons = await page.locator('.mud-nav-link .fas').count();
      console.log(`  FontAwesome icons found: ${icons}`);
      
      // Test expanding a nav group if present
      if (mudNavGroup > 0) {
        const firstGroup = page.locator('.mud-nav-group').first();
        await firstGroup.click();
        await page.waitForTimeout(500); // Wait for animation
        
        await page.screenshot({ 
          path: './playwright/screenshots/nav-menu-expanded.png',
          fullPage: true 
        });
        
        console.log('📸 Screenshots saved to playwright/screenshots/');
      }
      
      // Verify navigation is working
      expect(mudNavMenu).toBeGreaterThan(0);
      expect(mudNavLink).toBeGreaterThan(0);
      
      console.log('✅ MudBlazor navigation components found and working');
      
    } finally {
      await browser.close();
    }
  });
});