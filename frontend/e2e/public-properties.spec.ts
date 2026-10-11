import { expect, test } from '@playwright/test';

const property = {
  id: 'p1',
  propertyId: 245,
  blockId: 'b1',
  blockName: 'ChinaTown',
  propertyName: 'ChinaTown Apt 1',
  type: 'lowEndApartment',
  personCapacity: 2,
  rent: 2450,
  status: 'available',
  bedrooms: 2,
  bathrooms: 2,
  area: 1100,
  furnishingStatus: 'Furnished',
  amenities: ['Parking'],
  images: ['https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?w=900'],
  isFeatured: true,
  createdAt: new Date().toISOString(),
};

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/blocks/public', (route) =>
    route.fulfill({
      json: [
        {
          id: 'b1',
          blockId: 2,
          blockName: 'ChinaTown',
          numberOfProperties: 1,
          isActive: true,
          createdAt: '',
          updatedAt: '',
        },
      ],
    }),
  );
  await page.route('**/api/v1/properties/available**', (route) =>
    route.fulfill({
      json: {
        items: [property],
        page: 1,
        pageSize: 12,
        totalItems: 1,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    }),
  );
  await page.route('**/api/v1/properties/featured', (route) => route.fulfill({ json: [property] }));
});

test('visitor browses available properties', async ({ page }) => {
  await page.goto('/properties');
  await expect(page.getByRole('heading', { name: 'Find your place.' })).toBeVisible();
  await expect(page.getByText('ChinaTown Apt 1')).toBeVisible();
});

test('visitor submits an enquiry', async ({ page }) => {
  await page.route('**/api/v1/properties/p1', (route) => route.fulfill({ json: property }));
  await page.route('**/api/v1/enquiries', (route) =>
    route.fulfill({ status: 201, json: { id: 'e1', ...property } }),
  );
  await page.goto('/properties/p1');
  await page.getByLabel('Full name').fill('Jamie Reed');
  await page.getByLabel('Phone number').fill('555-0123');
  await page.getByRole('button', { name: 'Send enquiry' }).click();
  await expect(page.getByText('Thank you.')).toBeVisible();
});
