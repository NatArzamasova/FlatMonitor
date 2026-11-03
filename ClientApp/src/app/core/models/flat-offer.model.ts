export interface FlatOffer {
  district?: string;
  roomsCount?: number;
  priceEur?: number;
  originalPriceEur?: number;
  pricePerMeter?: number;
  livingAreaSqm?: number;
  url?: string;
  source?: string;
  /** When the flat is free, in the portal's words (`sofort`, `01.03.2027`, `vermietet`); from the detail page. */
  availability?: string;
  /** As the portal gives it: `A+` / `A_PLUS`, `A` … `H`; shown through `energyClassBadge()`. */
  energyEfficiencyClass?: string;
  /** Monthly Hausgeld (service charge) in whole euros. */
  serviceChargeEur?: number;
  /** Buyer's commission (Provision) in percent of the price, e.g. `3.57`; `0` = commission-free. */
  commissionPercent?: number;
}
