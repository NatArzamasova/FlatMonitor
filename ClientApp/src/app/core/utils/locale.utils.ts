import { DEFAULT_CURRENCY_CODE, LOCALE_ID, Provider } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeDe from '@angular/common/locales/de';

/**
 * The locale of every number, price and date in the app (the UI text stays English). Angular's pipes get it through
 * `provideAppLocale()`, the `Intl` formatters below and in the chart use it directly, so changing it switches all.
 */
export const APP_LOCALE = 'de-DE';
export const APP_CURRENCY = 'EUR';

const EURO_FORMAT = new Intl.NumberFormat(APP_LOCALE, {
  style: 'currency',
  currency: APP_CURRENCY,
  minimumFractionDigits: 0,
  maximumFractionDigits: 0
});
const DECIMAL_FORMAT = new Intl.NumberFormat(APP_LOCALE, { maximumFractionDigits: 1 });

/** Whole euros: `4200` → `4.200 €`. */
export function formatEuro(value: number): string {
  return EURO_FORMAT.format(value);
}

/** At most one decimal: `2.5` → `2,5`, `1200` → `1.200`. */
export function formatDecimal(value: number): string {
  return DECIMAL_FORMAT.format(value);
}

/** `LOCALE_ID` and the default currency for Angular's pipes; also for specs that check formatted text. */
export function provideAppLocale(): Provider[] {
  registerLocaleData(localeDe);
  return [
    { provide: LOCALE_ID, useValue: APP_LOCALE },
    { provide: DEFAULT_CURRENCY_CODE, useValue: APP_CURRENCY }
  ];
}
