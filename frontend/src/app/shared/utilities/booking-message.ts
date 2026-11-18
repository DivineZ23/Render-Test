import { propertyTypeLabel } from '../../core/constants/property-status.constants';
import { Property, PropertyBooking } from '../../core/models/property.models';

type BookingProperty = Pick<Property, 'propertyName' | 'blockName' | 'type'> & {
  images?: string[];
};

const BOOKING_CONTACT_IDS = [
  '336361433652527104', // Divine
  '441930552451530753', // Heede
] as const;

export function bookingMessage(
  property: BookingProperty,
  booking: Pick<PropertyBooking, 'monthlyRent'>,
  currentUserDiscordId: string | null | undefined,
): string {
  const rent = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(booking.monthlyRent);
  const contacts = [
    ...new Set(
      [currentUserDiscordId?.trim(), ...BOOKING_CONTACT_IDS].filter(
        (id): id is string => !!id && /^\d+$/.test(id),
      ),
    ),
  ];
  const pictures = (property.images ?? []).filter((url) => url.trim()).join(' | ');
  return [
    '**Company Name:** Imperial Estates',
    `**Property:** ${property.propertyName}`,
    `**Block:** ${property.blockName}`,
    `**Type:** ${propertyTypeLabel(property.type)}`,
    `**Rent:** ${rent}`,
    '**Number of People:** 0',
    `**Property Pictures:** ${pictures || 'Not available'}`,
    `**Contact Details:** ${contacts.map((id) => `<@${id}>`).join(' ')}`,
    '',
    '*Note: Contact any Imperial Estates agent in-city for booking this property.*',
  ].join('\n');
}
