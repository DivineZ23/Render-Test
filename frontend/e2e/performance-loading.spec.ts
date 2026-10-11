import { expect, Page, test } from '@playwright/test';
import { defaultAccessSettings } from '../src/app/core/constants/access-resource.constants';

const owner = {
  id: 'owner-1', username: 'owner', displayName: 'Owner', role: 'owner',
  approvalStatus: 'approved', accessStatus: 'active',
};
const snapshot = {
  id: 'snapshot-1', createdBy: 'owner-1', syncedAt: '2026-10-11T08:00:00Z',
  total: 1000, active: 900, overdue: 50, evictable: 30, empty: 20,
  unmappedTenants: 0, googleSheetSyncStatus: 'synced', records: [],
};
const summary = {
  totalBlocks: 1, totalProperties: 10, availableProperties: 4, bookedProperties: 2,
  occupiedProperties: 4, totalRevenue: 1000, totalCost: 400, totalProfit: 600,
  averageProfitPerProperty: 60, mostProfitableBlock: 'Central', mostProfitableBlockProfit: 600,
  pendingEnquiries: 0, pendingUsers: 0, recentStatusChanges: [],
};

async function setup(page: Page) {
  const requests: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/api/v1/')) requests.push(url.pathname + url.search);
  });
  await page.route('**/api/v1/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    const values: Record<string, unknown> = {
      '/api/v1/auth/me': owner,
      '/api/v1/access-management': defaultAccessSettings(),
      '/api/v1/properties/bookings/announcement-summary': { pendingCount: 0 },
      '/api/v1/notices/eviction-queue': [],
      '/api/v1/notices/snapshots': [snapshot],
      '/api/v1/team/agents': [owner],
    };
    await route.fulfill({ status: path in values ? 200 : 404, json: values[path] ?? { message: 'Unexpected API call in test' } });
  });
  return requests;
}

test('eviction queue loads without unrelated export/history requests', async ({ page }) => {
  const requests = await setup(page);
  await page.goto('/dashboard/notices/eviction-queue');
  await expect(page.getByRole('heading', { name: 'No properties awaiting eviction' })).toBeVisible();
  expect(requests.filter((path) => path.startsWith('/api/v1/notices/'))).toEqual(['/api/v1/notices/eviction-queue']);
  expect(requests.some((path) => path.startsWith('/api/v1/tenants/'))).toBe(false);
  expect(requests.filter((path) => path === '/api/v1/auth/me')).toHaveLength(1);
});

test('Sync History requests summaries and renders counts without full export rows', async ({ page }) => {
  const requests = await setup(page);
  await page.goto('/dashboard/notices/synced-data-records');
  await expect(page.getByRole('heading', { name: 'Sync History' })).toBeVisible();
  await expect(page.getByRole('cell', { name: '900', exact: true })).toBeVisible();
  expect(requests.filter((path) => path.startsWith('/api/v1/notices/'))).toEqual(['/api/v1/notices/snapshots?summaryOnly=true']);
});

test('dashboard renders placeholders before a slow response arrives', async ({ page }) => {
  await setup(page);
  let release!: () => void;
  const pending = new Promise<void>((resolve) => { release = resolve; });
  await page.route('**/api/v1/dashboard', async (route) => {
    await pending;
    await route.fulfill({ json: summary });
  });
  await page.goto('/dashboard');
  try {
    await expect(page.locator('[aria-label="Loading portfolio"]')).toBeVisible();
  } finally { release(); }
  await expect(page.getByText('Total revenue', { exact: true })).toBeVisible();
  await expect(page.locator('[aria-label="Loading portfolio"]')).toHaveCount(0);
});
