import { expect, Page, test } from '@playwright/test';
import { defaultAccessSettings } from '../src/app/core/constants/access-resource.constants';

async function setup(
  page: Page,
  clipboardDenied = false,
  currentUserDiscordId = '123456789012345678',
) {
  await page.addInitScript((denied) => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: async (text: string) => {
          if (denied) throw new Error('Clipboard access denied');
          (window as unknown as { copiedBooking: string }).copiedBooking = text;
        },
      },
    });
  }, clipboardDenied);
  const bookings = [1, 2].map((i) => ({
    id: `booking-${i}`,
    propertyId: 'property-1',
    fullName: `Customer ${i}`,
    cid: i,
    phoneNumber: '555-1234',
    discordId: `72707501248951094${i}`,
    monthlyRent: i * 10000,
    bookingAmount: 0,
    status: 'active',
    createdAt: '2026-11-18T12:30:00Z',
    createdByDisplayName: 'Test Agent',
  }));
  const property = {
    id: 'property-1',
    propertyId: 2,
    propertyName: 'Vespucci Boulevard 2',
    blockName: 'Vespucci Boulevard',
    type: 'trevorsBeachHouse',
    status: 'booked',
    rent: 99000,
    bookingCount: 2,
    images: ['https://img.example.test/one.jpg', 'https://img.example.test/two.jpg'],
  };
  const requests: string[] = [];
  await page.route('**/api/v1/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    requests.push(path);
    const responses: Record<string, unknown> = {
      '/api/v1/auth/me': {
        id: 'owner',
        discordUserId: currentUserDiscordId,
        role: 'owner',
        displayName: 'Owner',
        approvalStatus: 'approved',
        accessStatus: 'active',
      },
      '/api/v1/access-management': defaultAccessSettings(),
      '/api/v1/properties/bookings/announcement-summary': { pendingCount: 0 },
      '/api/v1/properties/bookings': [
        { ...property, propertyId: property.id, propertyNumber: 2, bookings },
      ],
      '/api/v1/properties/property-1/manage': property,
      '/api/v1/properties/property-1/bookings': bookings,
    };
    await route.fulfill({ status: path in responses ? 200 : 404, json: responses[path] ?? {} });
  });
  return requests;
}

for (const path of ['/dashboard/bookings', '/dashboard/properties/property-1/bookings']) {
  test(`copies each booking row with saved image links: ${path}`, async ({ page }) => {
    const requests = await setup(page);
    await page.goto(path);
    if (path === '/dashboard/bookings') await page.locator('summary').click();
    await expect(page.getByRole('button', { name: /^Copy booking for Customer/ })).toHaveCount(2);
    const countBeforeCopy = requests.length;
    for (const i of [1, 2]) {
      await page
        .getByRole('button', { name: `Copy booking for Customer ${i}`, exact: true })
        .click();
      const text = await page.evaluate(
        () => (window as unknown as { copiedBooking: string }).copiedBooking,
      );
      expect(text).toContain('**Company Name:** Imperial Estates');
      expect(text).toContain('**Property:** Vespucci Boulevard 2');
      expect(text).toContain("**Type:** Trevor's Beach House");
      expect(text).toContain(`**Rent:** $${i}0,000`);
      expect(text).toContain(
        '**Contact Details:** <@123456789012345678> <@336361433652527104> <@441930552451530753>',
      );
      expect(text).not.toContain(`<@72707501248951094${i}>`);
      expect(text).toContain('https://img.example.test/one.jpg | https://img.example.test/two.jpg');
      expect(text).toContain(
        '*Note: Contact any Imperial Estates agent in-city for booking this property.*',
      );
    }
    expect(requests.length).toBe(countBeforeCopy);
  });
}

test('reports clipboard failure and leaves Copy Booking usable', async ({ page }) => {
  await setup(page, true);
  await page.goto('/dashboard/bookings');
  await page.locator('summary').click();
  const button = page.getByRole('button', { name: 'Copy booking for Customer 1', exact: true });
  await button.click();
  await expect(
    page.getByText('Could not copy booking. Allow clipboard access and try again.'),
  ).toBeVisible();
  await expect(button).toBeEnabled();
});

test('copying as Divine mentions him only once', async ({ page }) => {
  await setup(page, false, '336361433652527104');
  await page.goto('/dashboard/bookings');
  await page.locator('summary').click();
  await page.getByRole('button', { name: 'Copy booking for Customer 1', exact: true }).click();
  const text = await page.evaluate(
    () => (window as unknown as { copiedBooking: string }).copiedBooking,
  );
  expect(text).toContain('**Contact Details:** <@336361433652527104> <@441930552451530753>');
  expect(text.match(/<@336361433652527104>/g)).toHaveLength(1);
});
