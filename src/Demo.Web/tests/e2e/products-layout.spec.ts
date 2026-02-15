import { expect, test, type Page } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/products?**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          {
            id: 'p-1',
            sku: 'SKU-001',
            name: 'Demo Item',
            category: 'Demo',
            price: 99,
            cost: 50,
            qtyOnHand: 12,
            reorderLevel: 5
          }
        ]
      })
    });
  });

  await page.route('**/api/inventory/low-stock', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [] })
    });
  });

  await page.route('**/api/products/*/barcodes', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] })
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id: 'b-1' })
    });
  });

  await page.route('**/api/products', async (route) => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'p-2' })
      });
      return;
    }

    await route.continue();
  });

  await page.route('**/api/products/*', async (route) => {
    if (route.request().method() === 'PUT') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      return;
    }

    await route.continue();
  });

  await page.route('**/api/inventory/in', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
});

async function inputsStayInsideLabels(page: Page, selector: string) {
  const ok = await page.evaluate((cssSelector) => {
    const controls = Array.from(document.querySelectorAll(cssSelector));
    return controls.every((el) => {
      const label = el.closest('label');
      if (!label) return false;
      const inputRect = el.getBoundingClientRect();
      const labelRect = label.getBoundingClientRect();
      return inputRect.left >= labelRect.left - 0.5 && inputRect.right <= labelRect.right + 0.5;
    });
  }, selector);

  expect(ok).toBeTruthy();
}

test('branded header is visible and product-form inputs do not overlap on mobile width', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'BarcodeOps Console' })).toBeVisible();
  await expect(page.getByText('Local MVP')).toBeVisible();

  await page.getByRole('button', { name: 'Products' }).click();
  await page.getByRole('button', { name: 'Create Product' }).click();

  await expect(page.getByRole('heading', { name: 'Create Product' })).toBeVisible();

  await inputsStayInsideLabels(page, '.create-grid input, .create-grid select, .create-grid textarea');
  await inputsStayInsideLabels(page, '.detail-grid input, .detail-grid select, .detail-grid textarea');

  await page.locator('.create-grid label', { hasText: 'SKU' }).locator('input').fill('SKU-E2E-001');
  await page.locator('.create-grid label', { hasText: 'Name' }).locator('input').fill('E2E Item');
  await page.locator('.section').filter({ hasText: 'Create Product' }).getByRole('button', { name: 'Create Product' }).click();

  await expect(page.getByText('Product SKU-E2E-001 created.')).toBeVisible();
});
