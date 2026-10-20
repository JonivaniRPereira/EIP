import { formatDate, formatNumber } from '@angular/common';
import { APP_LOCALE } from './locale';

describe('Brazilian display locale', () => {
  it('uses a dot for thousands and a comma for decimals', () => {
    expect(formatNumber(11110.5, APP_LOCALE, '1.2-2')).toBe('11.110,50');
  });
  it('displays day before month without changing the underlying timestamp', () => {
    expect(formatDate('2026-10-19T12:05:00Z', 'dd/MM/yyyy HH:mm', APP_LOCALE, 'UTC')).toBe(
      '19/10/2026 12:05',
    );
  });
});
