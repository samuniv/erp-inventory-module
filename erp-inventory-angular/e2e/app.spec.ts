import { test, expect } from '@playwright/test';

test.describe('ERP Inventory App', () => {
  test('should load the home page', async ({ page }) => {
    await page.goto('/');

    // Wait for Angular to load
    await page.waitForTimeout(2000);

    // Check if the app is running
    await expect(page.locator('app-root')).toBeVisible();

    // Check page title
    await expect(page).toHaveTitle(/ERP Inventory/);
  });

  test('should navigate to login page', async ({ page }) => {
    await page.goto('/');

    // Look for login button or link
    const loginButton = page
      .locator('button', { hasText: 'Login' })
      .or(page.locator('a', { hasText: 'Login' }));

    if ((await loginButton.count()) > 0) {
      await loginButton.click();
      await expect(page.url()).toContain('login');
    }
  });

  test('should have responsive design on mobile', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    await page.goto('/');
    await page.waitForTimeout(2000);

    // Check if app is still functional on mobile
    await expect(page.locator('app-root')).toBeVisible();
  });
});
