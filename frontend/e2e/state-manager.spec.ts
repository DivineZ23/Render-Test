import { expect, Page, test } from '@playwright/test';
import { defaultAccessSettings } from '../src/app/core/constants/access-resource.constants';

async function setup(page: Page, role = 'stateManager') {
  const settings = defaultAccessSettings();
  for (const key of [
    'commissions',
    'administration.users',
    'administration.accessManagement',
    'profile',
  ]) {
    settings.permissions[key].stateManager = true;
  }
  const user = {
    id: 'viewer',
    displayName: 'State Viewer',
    username: 'viewer',
    role,
    approvalStatus: 'approved',
    accessStatus: 'active',
  };
  const member = {
    id: 'agent',
    displayName: 'Test Agent',
    username: 'agent',
    role: 'agent',
    approvalStatus: 'approved',
    accessStatus: 'active',
    createdAt: '2026-10-12T00:00:00Z',
  };
  const requests: string[] = [];
  await page.route('**/api/v1/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    requests.push(`${request.method()} ${path}`);
    const values: Record<string, unknown> = {
      '/api/v1/auth/me': user,
      '/api/v1/access-management': settings,
      '/api/v1/users': { items: [member], totalItems: 1, page: 1, pageSize: 100 },
      '/api/v1/commissions': {
        agents: [
          {
            userId: 'agent',
            displayName: 'Test Agent',
            role: 'agent',
            outstandingCommission: 12000,
            outstandingCount: 1,
            auctionCount: 1,
            receivedCommission: 0,
          },
        ],
        totalOutstanding: 12000,
        totalReceived: 0,
        records: [
          {
            id: 'record',
            settlementId: 'sale',
            auctionReference: 'Test house',
            agentUserId: 'agent',
            agentDisplayName: 'Test Agent',
            agentRole: 'agent',
            isWinningAgent: false,
            baseShare: 0,
            premiumShare: 12000,
            commissionAmount: 12000,
            finalAuctionPrice: 110000,
            isPaid: false,
            createdAt: '2026-10-12T00:00:00Z',
          },
        ],
      },
      '/api/v1/properties/bookings/announcement-summary': { pendingCount: 0 },
    };
    await route.fulfill({ status: path in values ? 200 : 404, json: values[path] ?? {} });
  });
  return { settings, requests };
}

test('State Manager can view commissions without settlement or payment actions', async ({
  page,
}) => {
  const { requests } = await setup(page);
  await page.goto('/dashboard/commissions');
  await expect(page.getByRole('heading', { name: 'Agent commissions', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Test Agent Agent', exact: true })).toBeVisible();
  await expect(
    page.getByRole('button', {
      name: /Record settlement|Mark paid|Edit settlement|Delete settlement/,
    }),
  ).toHaveCount(0);
  expect(requests.some((path) => path.includes('announcement-summary'))).toBe(false);
  expect(requests.every((path) => path.startsWith('GET '))).toBe(true);
});

test('denied direct navigation redirects to an allowed page without loading denied data', async ({
  page,
}) => {
  const { requests } = await setup(page);
  await page.goto('/dashboard/audit-logs');
  await expect(page).toHaveURL(/\/dashboard\/commissions$/);
  expect(requests.some((path) => path.includes('audit-logs'))).toBe(false);
  await page.goto('/dashboard/properties/new');
  await expect(page).toHaveURL(/\/dashboard\/commissions$/);
});

test('State Manager sees user roles and access settings but cannot edit them', async ({ page }) => {
  await setup(page);
  await page.goto('/dashboard/users');
  await expect(page.getByRole('heading', { name: 'Team access' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'State Managers', exact: true })).toBeVisible();
  await expect(page.getByRole('combobox')).toHaveCount(0);
  await expect(
    page.getByRole('button', { name: /^(Approve|Reject|Revoke|Delete|Restore)$/ }),
  ).toHaveCount(0);
  await page.goto('/dashboard/access-management');
  await expect(
    page.getByRole('columnheader', { name: 'State Manager', exact: true }),
  ).toBeVisible();
  expect(await page.getByRole('checkbox').count()).toBeGreaterThan(0);
  await expect(page.locator('input[type=checkbox]:enabled')).toHaveCount(0);
});

test('Owner can assign State Manager and grant view pages, never write workflows', async ({
  page,
}) => {
  await setup(page, 'owner');
  let roleChange: unknown;
  await page.route('**/api/v1/users/agent/role', async (route) => {
    roleChange = route.request().postDataJSON();
    await route.fulfill({ json: { id: 'agent', role: 'stateManager' } });
  });
  await page.goto('/dashboard/users');
  await expect(page.getByRole('combobox', { name: 'Role for Test Agent' })).toBeEnabled();
  await expect(page.getByRole('combobox').locator('option[value="stateManager"]')).toHaveText(
    'State Manager',
  );
  await page.getByRole('combobox', { name: 'Role for Test Agent' }).selectOption('stateManager');
  await expect(page.getByRole('dialog')).toContainText('strictly view only');
  await expect(page.getByRole('button', { name: 'Change role', exact: true })).toBeDisabled();
  await page.getByPlaceholder('Add a clear reason').fill('State oversight');
  await page.getByRole('button', { name: 'Change role', exact: true }).click();
  await expect.poll(() => roleChange).toEqual({ role: 'stateManager', reason: 'State oversight' });
  await page.goto('/dashboard/access-management');
  await expect(
    page.getByRole('checkbox', { name: 'Agent Analytics access for State Manager', exact: true }),
  ).toBeEnabled();
  await expect(
    page.getByRole('checkbox', { name: 'Book Property access for State Manager', exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole('checkbox', { name: 'Create Listing access for State Manager', exact: true }),
  ).toBeDisabled();
});

test('State Manager profile is view only', async ({ page }) => {
  await setup(page);
  await page.goto('/dashboard/profile');
  await expect(page.getByRole('heading', { name: 'Personal information' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Edit', exact: true })).toHaveCount(0);
  await expect(page.getByRole('note')).toContainText('View-only access');
});
