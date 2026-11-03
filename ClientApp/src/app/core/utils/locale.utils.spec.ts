import { TestBed } from '@angular/core/testing';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { DEFAULT_CURRENCY_CODE, LOCALE_ID } from '@angular/core';
import { APP_CURRENCY, APP_LOCALE, formatDecimal, formatEuro, formatPercent, provideAppLocale } from './locale.utils';

describe('locale.utils', () => {
  it('uses German formats', () => {
    expect(APP_LOCALE).toBe('de-DE');
    expect(APP_CURRENCY).toBe('EUR');
  });

  describe('formatEuro', () => {
    it('formats whole euros in the app locale', () => {
      expect(formatEuro(4200)).toBe('4.200 €');
      expect(formatEuro(4200.6)).toBe('4.201 €');
      expect(formatEuro(12)).toBe('12 €');
    });

    it('follows APP_LOCALE', () => {
      const expected = new Intl.NumberFormat(APP_LOCALE, { style: 'currency', currency: APP_CURRENCY, maximumFractionDigits: 0 });

      expect(formatEuro(1234567)).toBe(expected.format(1234567));
    });
  });

  describe('formatDecimal', () => {
    it('formats at most one decimal with a comma and groups thousands', () => {
      expect(formatDecimal(2.5)).toBe('2,5');
      expect(formatDecimal(65.55)).toBe('65,6');
      expect(formatDecimal(3)).toBe('3');
      expect(formatDecimal(1200)).toBe('1.200');
    });
  });

  describe('formatPercent', () => {
    it('formats up to three decimals with a comma and a percent sign', () => {
      expect(formatPercent(3.57)).toBe('3,57 %');
      expect(formatPercent(2.975)).toBe('2,975 %');
      expect(formatPercent(0)).toBe('0 %');
    });
  });

  describe('provideAppLocale', () => {
    beforeEach(() => TestBed.configureTestingModule({ providers: [provideAppLocale()] }));

    it('sets LOCALE_ID and the default currency', () => {
      expect(TestBed.inject(LOCALE_ID)).toBe(APP_LOCALE);
      expect(TestBed.inject(DEFAULT_CURRENCY_CODE)).toBe(APP_CURRENCY);
    });

    it('makes Angular\'s pipes format numbers, prices and dates in German', () => {
      TestBed.runInInjectionContext(() => {
        const locale = TestBed.inject(LOCALE_ID);
        const currency = new CurrencyPipe(locale, TestBed.inject(DEFAULT_CURRENCY_CODE));

        expect(currency.transform(4200, undefined, 'symbol', '1.0-0')).toBe('4.200 €');
        expect(new DecimalPipe(locale).transform(65.5, '1.0-1')).toBe('65,5');
        expect(new DatePipe(locale).transform('2024-01-15T10:00:00Z', 'short', 'UTC')).toBe('15.01.24, 10:00');
        expect(new DatePipe(locale).transform('2024-01-15T10:00:00Z', 'medium', 'UTC')).toBe('15.01.2024, 10:00:00');
      });
    });
  });
});
