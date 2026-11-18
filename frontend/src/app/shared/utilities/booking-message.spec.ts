import { bookingMessage } from './booking-message';

describe('booking message', () => {
  const property = {
    propertyName: 'Vespucci Boulevard 2',
    blockName: 'Vespucci Boulevard',
    type: 'trevorsBeachHouse' as const,
    images: ['https://img.example.test/one.jpg', 'https://img.example.test/two.jpg'],
  };
  it('uses the row rent and mentions the current user, Divine and Heede', () => {
    expect(bookingMessage(property, { monthlyRent: 10000 }, '727075012489510944')).toBe(
      [
        '**Company Name:** Imperial Estates',
        '**Property:** Vespucci Boulevard 2',
        '**Block:** Vespucci Boulevard',
        "**Type:** Trevor's Beach House",
        '**Rent:** $10,000',
        '**Number of People:** 0',
        '**Property Pictures:** https://img.example.test/one.jpg | https://img.example.test/two.jpg',
        '**Contact Details:** <@727075012489510944> <@336361433652527104> <@441930552451530753>',
        '',
        '*Note: Contact any Imperial Estates agent in-city for booking this property.*',
      ].join('\n'),
    );
  });
  it('handles missing images and invalid Discord IDs without malformed mentions', () => {
    const message = bookingMessage({ ...property, images: [] }, { monthlyRent: 0 }, 'invalid');
    expect(message).toContain('**Rent:** $0');
    expect(message).toContain('**Property Pictures:** Not available');
    expect(message).toContain('**Contact Details:** <@336361433652527104> <@441930552451530753>');
    expect(message).not.toContain('<@invalid>');
  });
  it.each(['336361433652527104', '441930552451530753'])(
    'does not duplicate contact %s when they copy',
    (id) => {
      const message = bookingMessage(property, { monthlyRent: 10000 }, id);
      expect(message.match(/<@336361433652527104>/g)).toHaveLength(1);
      expect(message.match(/<@441930552451530753>/g)).toHaveLength(1);
      expect(message).toContain(`**Contact Details:** <@${id}>`);
    },
  );
});
