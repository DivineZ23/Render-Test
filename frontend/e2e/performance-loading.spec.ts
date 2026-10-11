import { expect, Page, test } from '@playwright/test';
import { defaultAccessSettings } from '../src/app/core/constants/access-resource.constants';

const owner = {
  id: 'owner-1',
  username: 'owner',
  displayName: 'Owner',
  role: 'owner',
  approvalStatus: 'approved',
  accessStatus: 'active',
};
const snapshot = {
  id: 'snapshot-1',
  createdBy: 'owner-1',
  syncedAt: '2026-10-11T08:00:00Z',
  total: 1000,
  active: 900,
  overdue: 50,
  evictable: 30,
  empty: 20,
  unmappedTenants: 0,
  googleSheetSyncStatus: 'synced',
  records: [],
};
const summary = {
  totalBlocks: 1,
  totalProperties: 10,
  availableProperties: 4,
  bookedProperties: 2,
  occupiedProperties: 4,
  totalRevenue: 1000,
  totalCost: 400,
  totalProfit: 600,
  averageProfitPerProperty: 60,
  mostProfitableBlock: 'Central',
  mostProfitableBlockProfit: 600,
  pendingEnquiries: 0,
  pendingUsers: 0,
  recentStatusChanges: [],
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
    await route.fulfill({
      status: path in values ? 200 : 404,
      json: values[path] ?? { message: 'Unexpected API call in test' },
    });
  });
  return requests;
}

test('eviction queue loads without unrelated export/history requests', async ({ page }) => {
  const requests = await setup(page);
  await page.goto('/dashboard/notices/eviction-queue');
  await expect(
    page.getByRole('heading', { name: 'No properties awaiting eviction' }),
  ).toBeVisible();
  expect(requests.filter((path) => path.startsWith('/api/v1/notices/'))).toEqual([
    '/api/v1/notices/eviction-queue',
  ]);
  expect(requests.some((path) => path.startsWith('/api/v1/tenants/'))).toBe(false);
  expect(requests.filter((path) => path === '/api/v1/auth/me')).toHaveLength(1);
});

test('Sync History requests summaries and renders counts without full export rows', async ({
  page,
}) => {
  const requests = await setup(page);
  await page.goto('/dashboard/notices/synced-data-records');
  await expect(page.getByRole('heading', { name: 'Sync History' })).toBeVisible();
  await expect(page.getByRole('cell', { name: '900', exact: true })).toBeVisible();
  expect(requests.filter((path) => path.startsWith('/api/v1/notices/'))).toEqual([
    '/api/v1/notices/snapshots?summaryOnly=true',
  ]);
});

test('dashboard renders placeholders before a slow response arrives', async ({ page }) => {
  await setup(page);
  let release!: () => void;
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/v1/dashboard', async (route) => {
    await pending;
    await route.fulfill({ json: summary });
  });
  await page.goto('/dashboard');
  try {
    await expect(page.locator('[aria-label="Loading portfolio"]')).toBeVisible();
  } finally {
    release();
  }
  await expect(page.getByText('Total revenue', { exact: true })).toBeVisible();
  await expect(page.locator('[aria-label="Loading portfolio"]')).toHaveCount(0);
});

test('returning to a page reuses its model, then updates it without a blocking loader', async ({
  page,
}) => {
  const requests = await setup(page);
  const time = new Date('2026-10-11T12:00:00Z');
  await page.clock.setFixedTime(time);
  let calls = 0;
  let release!: () => void;
  const refresh = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/v1/dashboard', async (route) => {
    calls++;
    if (calls > 1) await refresh;
    await route.fulfill({ json: { ...summary, totalRevenue: calls === 1 ? 1000 : 2500 } });
  });
  const revenue = page.locator('.financial-stats article').first().locator('b');
  const away = () => page.locator('a[href="/dashboard/notices/synced-data-records"]').click();
  const back = () =>
    page.getByRole('link', { name: 'Imperial Estates dashboard', exact: true }).click();
  await page.goto('/dashboard');
  await expect(revenue).toHaveText('$1,000');
  await away();
  await expect(page.getByRole('heading', { name: 'Sync History' })).toBeVisible();
  await back();
  await expect(revenue).toHaveText('$1,000');
  expect(calls).toBe(1);
  await away();
  await page.clock.setFixedTime(new Date(time.getTime() + 11_000));
  await back();
  try {
    await expect(revenue).toHaveText('$1,000');
    await expect(page.locator('app-data-refresh-status')).toContainText('Showing saved data');
    await expect(page.locator('[aria-label="Loading portfolio"]')).toHaveCount(0);
  } finally {
    release();
  }
  await expect(revenue).toHaveText('$2,500');
  await expect(page.locator('app-data-refresh-status .refresh-status')).toHaveCount(0);
  expect(requests.filter((path) => path === '/api/v1/dashboard')).toHaveLength(2);
});

test('a failed background refresh keeps the preview and offers a working reload', async ({
  page,
}) => {
  await setup(page);
  const time = new Date('2026-10-11T12:00:00Z');
  await page.clock.setFixedTime(time);
  let calls = 0;
  await page.route('**/api/v1/dashboard', async (route) => {
    calls++;
    await route.fulfill(
      calls === 2
        ? { status: 503, json: { message: 'Temporary outage' } }
        : { json: { ...summary, totalRevenue: calls === 1 ? 1000 : 3000 } },
    );
  });
  const revenue = page.locator('.financial-stats article').first().locator('b');
  await page.goto('/dashboard');
  await expect(revenue).toHaveText('$1,000');
  await page.locator('a[href="/dashboard/notices/synced-data-records"]').click();
  await expect(page.getByRole('heading', { name: 'Sync History' })).toBeVisible();
  await page.clock.setFixedTime(new Date(time.getTime() + 11_000));
  await page.getByRole('link', { name: 'Imperial Estates dashboard', exact: true }).click();
  await expect(page.locator('app-data-refresh-status')).toContainText('Refresh failed');
  await expect(revenue).toHaveText('$1,000');
  await expect(page.getByText('Temporary outage', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Reload', exact: true }).click();
  await expect(revenue).toHaveText('$3,000');
});

test('eviction notices render a bounded page, load older notices, and resolve one record', async ({
  page,
}) => {
  const requests = await setup(page);
  const record = (rowNumber: number) => ({
    rowNumber,
    status: 'evictable',
    address: `House ${rowNumber}`,
    interior: 'Apartment',
    income: 2000,
    cost: 1000,
    tenantMatched: true,
    isResolved: false,
    evictionNotice: `Eviction notice for House ${rowNumber}`,
    discordId: '123456789',
  });
  let release!: () => void;
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/v1/notices/feed?**', async (route) => {
    await pending;
    const skip = Number(new URL(route.request().url()).searchParams.get('skip'));
    await route.fulfill({
      json: {
        asOf: snapshot.syncedAt,
        items: Array.from({ length: skip ? 1 : 50 }, (_, i) => ({
          snapshotId: snapshot.id,
          syncedAt: snapshot.syncedAt,
          record: record(skip + i + 1),
        })),
        hasMore: !skip,
        nextSkip: skip ? 51 : 50,
      },
    });
  });
  let releaseTotals!: () => void;
  const totalsPending = new Promise<void>((resolve) => {
    releaseTotals = resolve;
  });
  await page.route('**/api/v1/notices/feed-summary?**', async (route) => {
    await totalsPending;
    await route.fulfill({
      json: {
        syncedAt: snapshot.syncedAt,
        currentTotal: 51,
        totalNotices: 51,
        pending: 51,
        sentToday: 0,
      },
    });
  });
  await page.route('**/api/v1/notices/snapshots/*/records/1/resolution-record', (route) =>
    route.fulfill({
      json: {
        ...record(1),
        isResolved: true,
        resolvedByDisplayName: 'Owner',
        resolvedAt: new Date().toISOString(),
      },
    }),
  );
  await page.goto('/dashboard/notices/eviction');
  try {
    await expect(page.getByRole('status')).toHaveText('Loading eviction notices…');
    await expect(page.getByText('No synced data yet')).toHaveCount(0);
  } finally {
    release();
  }
  try {
    await expect(page.locator('.notice-card')).toHaveCount(50);
    await expect(page.getByLabel('Loading notice totals')).toBeVisible();
  } finally {
    releaseTotals();
  }
  await expect(page.getByText('50 of 51 notices loaded')).toBeVisible();
  await page.getByRole('button', { name: 'Load more', exact: true }).click();
  await expect(page.locator('.notice-card')).toHaveCount(51);
  await page.getByRole('checkbox', { name: 'Mark as resolved' }).first().check();
  await expect(page.locator('.resolved-by').first()).toContainText('Owner');
  await expect(page.locator('.notice-summary .pending b')).toHaveText('50');
  expect(requests.filter((path) => path.startsWith('/api/v1/notices/feed?'))).toHaveLength(2);
  expect(requests.some((path) => path === '/api/v1/notices/snapshots?summaryOnly=false')).toBe(
    false,
  );
  expect(requests.some((path) => path === '/api/v1/notices/eviction-queue')).toBe(false);
});

test('overdue list retries a failed page without displaying a false empty state', async ({
  page,
}) => {
  const requests = await setup(page);
  let attempts = 0;
  await page.route('**/api/v1/notices/records?**', async (route) => {
    attempts++;
    await route.fulfill(
      attempts === 1
        ? { status: 503, json: { message: 'Temporarily unavailable' } }
        : {
            json: {
              snapshot: {
                ...snapshot,
                records: [
                  {
                    rowNumber: 1,
                    status: 'overdue',
                    address: 'Marina Drive 8',
                    interior: 'Apartment',
                    income: 2000,
                    cost: 1000,
                    tenantMatched: false,
                    isResolved: false,
                  },
                ],
              },
              hasMore: false,
              nextSkip: 1,
            },
          },
    );
  });
  await page.goto('/dashboard/notices/overdue-list');
  await expect(page.getByRole('alert')).toContainText('Property records could not be loaded.');
  await expect(page.getByText('No synced data yet')).toHaveCount(0);
  await page.getByRole('button', { name: 'Retry', exact: true }).click();
  await expect(page.getByRole('cell', { name: 'Marina Drive 8' })).toBeVisible();
  expect(requests.filter((path) => path.startsWith('/api/v1/notices/records?'))).toHaveLength(2);
  expect(requests.some((path) => path.startsWith('/api/v1/notices/snapshot'))).toBe(false);
});
