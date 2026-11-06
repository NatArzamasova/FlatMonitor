import { Component, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { FlatsService } from '../../../../core/services/flats.service';
import { ArchivedOffer } from '../../../../core/models/archived-offer.model';
import { energyClassRank } from '../../../../core/utils/energy-class.utils';
import { TableSort } from '../../../../core/utils/table-sort';
import { archivedPreviewUrl } from '../../../../core/utils/preview.utils';
import { daysListed, filterByLastSeen, lastSeenPeriodLabel } from '../../../../core/utils/archive.utils';
import { FilterChip } from '../../../../core/utils/filter-chips.utils';
import { OFFER_SORT_COLUMNS, OfferRow, OfferSortColumn, SortColumnOption } from '../offers-tab/offer-row';
import { LastSeenFilter, OFFER_LIST_IMPORTS, OFFER_LIST_STYLES, OfferListTab } from '../offers-tab/offer-list-tab';

/** An archived offer with its listing time, computed once per load so the table can sort by it. */
export type ArchiveListOffer = ArchivedOffer & { daysListed: number | null };

export type ArchiveSortColumn = OfferSortColumn | 'firstSeenUtc' | 'lastSeenUtc' | 'daysListed';

/** The columns of Current Offers, with the archive's three before Source, as in the table. */
const ARCHIVE_SORT_COLUMNS: readonly SortColumnOption<ArchiveSortColumn>[] = [
  ...OFFER_SORT_COLUMNS.filter(item => item.column !== 'source'),
  { column: 'firstSeenUtc', label: 'First Seen' },
  { column: 'lastSeenUtc', label: 'Last Seen' },
  { column: 'daysListed', label: 'Days Listed' },
  ...OFFER_SORT_COLUMNS.filter(item => item.column === 'source')
];

/**
 * The flats no longer offered (`GET /api/flats/archived-offers`): the Current Offers table with first seen, last
 * seen and days listed, and a "Last seen" filter. Shares the template and filters of Current Offers
 * (`OfferListTab`); sorted by last seen, newest first, until another column is chosen.
 */
@Component({
  selector: 'app-archive-tab',
  standalone: true,
  host: { class: 'fill-column' },
  imports: OFFER_LIST_IMPORTS,
  templateUrl: '../offers-tab/offers-tab.component.html',
  styles: [OFFER_LIST_STYLES]
})
export class ArchiveTabComponent extends OfferListTab<ArchiveListOffer, ArchiveSortColumn> {
  private readonly flatsService = inject(FlatsService);

  readonly archived = true;
  readonly lastSeen: LastSeenFilter = { days: null };
  readonly sortColumns = ARCHIVE_SORT_COLUMNS;

  // Energy classes sort by the scale (A+ first), not alphabetically; unknown ones last. ISO dates sort as text.
  readonly sort = new TableSort<ArchiveListOffer, ArchiveSortColumn>({
    energyEfficiencyClass: offer => energyClassRank(offer.energyEfficiencyClass)
  });

  protected readonly loadErrorTexts = { fallback: 'Failed to load archived offers', httpPrefix: 'Error loading archived offers' };

  constructor() {
    super();
    this.sort.column = 'lastSeenUtc';
    this.sort.direction = 'desc';
  }

  protected fetchOffers(): Observable<ArchiveListOffer[]> {
    return this.flatsService.getArchivedOffers()
      .pipe(map(offers => offers.map(offer => ({ ...offer, daysListed: daysListed(offer) }))));
  }

  protected override applyExtraFilters(offers: ArchiveListOffer[]): ArchiveListOffer[] {
    return filterByLastSeen(offers, this.lastSeen.days);
  }

  protected override extraFilterChips(): FilterChip[] {
    const days = this.lastSeen.days;
    return days === null ? [] : [{ kind: 'lastSeenWithin', value: days, label: `seen in the ${lastSeenPeriodLabel(days)}` }];
  }

  protected override removeExtraFilterChip(chip: FilterChip): void {
    if (chip.kind === 'lastSeenWithin') {
      this.lastSeen.days = null;
    }
  }

  protected override resetExtraFilters(): void {
    this.lastSeen.days = null;
  }

  /** The archived photo, and when the flat was seen. */
  protected override toRow(offer: ArchiveListOffer): OfferRow<ArchiveListOffer> {
    return {
      ...super.toRow(offer),
      previewUrl: archivedPreviewUrl(offer),
      seen: { firstSeenUtc: offer.firstSeenUtc, lastSeenUtc: offer.lastSeenUtc, daysListed: offer.daysListed }
    };
  }
}
