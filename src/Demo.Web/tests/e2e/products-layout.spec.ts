import { expect, test, type Page } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  const products = [
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
  ];

  const productBarcodes = new Map<string, Array<{ id: string; productId: string; format: string; codeValue: string; isPrimary: boolean }>>();

  await page.route('**/api/products?**', async (route) => {
    const url = new URL(route.request().url());
    const keyword = (url.searchParams.get('keyword') ?? '').toLowerCase();
    const filtered = keyword
      ? products.filter((p) => p.sku.toLowerCase().includes(keyword) || p.name.toLowerCase().includes(keyword))
      : products;

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: filtered })
    });
  });

  await page.route('**/api/inventory/low-stock', async (route) => {
    const items = products
      .filter((p) => p.qtyOnHand <= p.reorderLevel)
      .map((p) => ({
        productId: p.id,
        sku: p.sku,
        name: p.name,
        qtyOnHand: p.qtyOnHand,
        reorderLevel: p.reorderLevel
      }));

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items }) });
  });

  await page.route('**/api/products/*/barcodes', async (route) => {
    const request = route.request();
    const productId = request.url().split('/api/products/')[1]?.split('/barcodes')[0] ?? '';

    if (request.method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: productBarcodes.get(productId) ?? [] })
      });
      return;
    }

    const payload = JSON.parse(request.postData() ?? '{}') as { format?: string; codeValue?: string; isPrimary?: boolean };
    const list = productBarcodes.get(productId) ?? [];
    list.push({
      id: `b-${list.length + 1}`,
      productId,
      format: payload.format ?? 'CODE_128',
      codeValue: payload.codeValue ?? '',
      isPrimary: Boolean(payload.isPrimary)
    });
    productBarcodes.set(productId, list);

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: list[list.length - 1].id }) });
  });

  await page.route('**/api/products', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }

    const payload = JSON.parse(route.request().postData() ?? '{}') as {
      sku: string;
      name: string;
      category: string | null;
      price: number;
      cost: number;
      initialQty: number;
      reorderLevel: number;
    };

    products.push({
      id: `p-${products.length + 1}`,
      sku: payload.sku,
      name: payload.name,
      category: payload.category ?? undefined,
      price: payload.price,
      cost: payload.cost,
      qtyOnHand: payload.initialQty,
      reorderLevel: payload.reorderLevel
    });

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: products[products.length - 1].id }) });
  });

  await page.route('**/api/products/*', async (route) => {
    if (route.request().method() !== 'PUT') {
      await route.continue();
      return;
    }

    const request = route.request();
    const productId = request.url().split('/api/products/')[1] ?? '';
    const payload = JSON.parse(request.postData() ?? '{}') as {
      name: string;
      category: string | null;
      price: number;
      cost: number;
      reorderLevel: number;
    };

    const target = products.find((p) => p.id === productId);
    if (target) {
      target.name = payload.name;
      target.category = payload.category ?? undefined;
      target.price = payload.price;
      target.cost = payload.cost;
      target.reorderLevel = payload.reorderLevel;
    }

    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  await page.route('**/api/inventory/in', async (route) => {
    const payload = JSON.parse(route.request().postData() ?? '{}') as { productId: string; qty: number };
    const target = products.find((p) => p.id === payload.productId);
    if (target) {
      target.qtyOnHand += payload.qty;
    }

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

test('products flow: search, update, add barcode, and stock in', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Products' }).click();

  await page.getByPlaceholder('Search by SKU or name').fill('SKU-001');
  await page.getByRole('button', { name: 'Search' }).click();
  await expect(page.getByRole('cell', { name: 'SKU-001' })).toBeVisible();

  await page.getByRole('cell', { name: 'SKU-001' }).click();
  await page.locator('.right-panel label', { hasText: 'Name' }).locator('input').fill('Demo Item Updated');
  await page.locator('.right-panel').getByRole('button', { name: 'Update Product' }).click();
  await expect(page.getByText('Product updated.')).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Demo Item Updated' })).toBeVisible();

  await page.locator('.right-panel label', { hasText: 'Format' }).locator('select').selectOption('EAN_13');
  await page.locator('.right-panel label', { hasText: 'Code Value' }).locator('input').fill('471234567890');
  await page.locator('.right-panel').getByRole('button', { name: 'Add Barcode' }).click();
  await expect(page.getByText('Barcode EAN_13 added.')).toBeVisible();
  await expect(page.getByRole('cell', { name: '471234567890' })).toBeVisible();

  await page.locator('.right-panel label', { hasText: 'Qty' }).locator('input').fill('7');
  await page.locator('.right-panel').getByRole('button', { name: 'Stock In' }).click();
  await expect(page.getByText('Stock in +7 completed.')).toBeVisible();
  await expect(page.getByRole('cell', { name: '19' })).toBeVisible();
});
