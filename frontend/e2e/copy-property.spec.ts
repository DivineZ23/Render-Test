import { expect, Page, test } from '@playwright/test';
import { defaultAccessSettings } from '../src/app/core/constants/access-resource.constants';

async function setup(
  page: Page,
  bookingCount = 2,
  highestRent: number | null = 20000,
  denied = false,
) {
  await page.addInitScript((deny) => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: async (text: string) => {
          if (deny) throw new Error('Clipboard denied');
          (window as unknown as { copiedProperty: string }).copiedProperty = text;
        },
      },
    });
  }, denied);
  const requests: string[] = [];
  await page.route('**/api/v1/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    requests.push(`${route.request().method()} ${path}`);
    const responses: Record<string, unknown> = {
      '/api/v1/auth/me': {
        id: 'owner',
        discordUserId: '123456789012345678',
        role: 'owner',
        displayName: 'Owner',
        approvalStatus: 'approved',
        accessStatus: 'active',
      },
      '/api/v1/access-management': defaultAccessSettings(),
      '/api/v1/properties/bookings/announcement-summary': { pendingCount: 0 },
      '/api/v1/blocks': [],
      '/api/v1/properties': {
        items: [
          {
            id: 'property-1',
            propertyId: 2,
            propertyName: 'Vespucci Boulevard 2',
            blockName: 'Vespucci Boulevard',
            type: 'trevorsBeachHouse',
            status: 'available',
            rent: 0,
            bookingCount: 0,
            images: ['https://img.example.test/one.jpg', 'https://img.example.test/two.jpg'],
          },
        ],
        page: 1,
        pageSize: 10,
        totalItems: 1,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      },
      '/api/v1/properties/property-1/booking-summary': { bookingCount, highestRent },
    };
    await route.fulfill({ status: path in responses ? 200 : 404, json: responses[path] ?? {} });
  });
  await page.goto('/dashboard/properties');
  await expect(copyButton(page)).toBeVisible();
  return requests;
}

const copyButton = (page: Page) =>
  page.getByRole('button', { name: 'Copy Vespucci Boulevard 2', exact: true });
const copied = (page: Page) =>
  page.evaluate(() => (window as unknown as { copiedProperty?: string }).copiedProperty);

test('tiny icon loads current booking count and highest rent only when clicked', async ({
  page,
}) => {
  const requests = await setup(page);
  expect(requests.some((path) => path.includes('booking-summary'))).toBe(false);
  const bounds = await copyButton(page).boundingBox();
  expect(bounds?.width).toBeLessThanOrEqual(28);
  await copyButton(page).click();
  await expect(page.getByText('Property booking message copied.')).toBeVisible();
  expect(await copied(page)).toContain('**Rent:** $20,000');
  expect(await copied(page)).toContain('**Number of Interested People:** 2');
  expect(await copied(page)).toContain(
    'https://img.example.test/one.jpg | https://img.example.test/two.jpg',
  );
  expect(await copied(page)).toContain(
    '**Contact Details:** <@123456789012345678> <@336361433652527104> <@441930552451530753>',
  );
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(requests.filter((path) => path.includes('booking-summary'))).toHaveLength(1);
});

for (const rent of ['0', '12500.50']) {
  test(`asks for rent without bookings, validates input and copies $${rent}`, async ({ page }) => {
    const requests = await setup(page, 0, null);
    await copyButton(page).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    const input = dialog.getByRole('spinbutton', { name: 'Rent ($)' });
    const submit = dialog.getByRole('button', { name: 'Copy to clipboard' });
    await expect(submit).toBeDisabled();
    await input.fill('-1');
    await expect(submit).toBeDisabled();
    await input.fill('1.234');
    await expect(submit).toBeDisabled();
    await input.fill(rent);
    await submit.click();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByText('Property booking message copied.')).toBeVisible();
    expect(await copied(page)).toContain(`**Rent:** $${rent === '0' ? '0' : '12,500.5'}`);
    expect(await copied(page)).toContain('**Number of Interested People:** 0');
    expect(requests.every((path) => path.startsWith('GET '))).toBe(true);
  });
}

test('cancel leaves clipboard unchanged and icon usable', async ({ page }) => {
  await setup(page, 0, null);
  await copyButton(page).click();
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(await copied(page)).toBeUndefined();
  await expect(copyButton(page)).toBeEnabled();
});

test('zero rent from existing bookings copies without a rent prompt', async ({ page }) => {
  await setup(page, 1, 0);
  await copyButton(page).click();
  await expect(page.getByText('Property booking message copied.')).toBeVisible();
  expect(await copied(page)).toContain('**Rent:** $0');
  expect(await copied(page)).toContain('**Number of Interested People:** 1');
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('clipboard failures allow retry', async ({ page }) => {
  await setup(page, 2, 20000, true);
  await copyButton(page).click();
  await expect(
    page.getByText('Could not copy. Allow clipboard access and try again.'),
  ).toBeVisible();
  await expect(copyButton(page)).toBeEnabled();
});

test('booking fetch failures do not copy stale data and allow retry', async ({ page }) => {
  await setup(page);
  await page.route('**/properties/property-1/booking-summary', (route) =>
    route.fulfill({ status: 500, json: {} }),
  );
  await copyButton(page).click();
  await expect(
    page.getByText('Could not load current booking details. Please try again.'),
  ).toBeVisible();
  expect(await copied(page)).toBeUndefined();
  await expect(copyButton(page)).toBeEnabled();
});
