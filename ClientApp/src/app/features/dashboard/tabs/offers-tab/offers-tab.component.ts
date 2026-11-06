import { Component, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { FlatsService } from '../../../../core/services/flats.service';
import { FlatOffer } from '../../../../core/models/flat-offer.model';
import { energyClassRank } from '../../../../core/utils/energy-class.utils';
import { TableSort } from '../../../../core/utils/table-sort';
import { OFFER_SORT_COLUMNS, OfferSortColumn } from './offer-row';
import { LastSeenFilter, OFFER_LIST_IMPORTS, OFFER_LIST_STYLES, OfferListTab } from './offer-list-tab';

/** The flats offered now (FlatsSnapshots). */
@Component({
  selector: 'app-offers-tab',
  standalone: true,
  host: { class: 'fill-column' },
  imports: OFFER_LIST_IMPORTS,
  templateUrl: './offers-tab.component.html',
  styles: [OFFER_LIST_STYLES]
})
export class OffersTabComponent extends OfferListTab<FlatOffer, OfferSortColumn> {
  private readonly flatsService = inject(FlatsService);

  readonly archived = false;
  readonly lastSeen: LastSeenFilter | null = null;
  readonly sortColumns = OFFER_SORT_COLUMNS;

  // Energy classes sort by the scale (A+ first), not alphabetically; unknown ones last.
  readonly sort = new TableSort<FlatOffer, OfferSortColumn>({
    energyEfficiencyClass: offer => energyClassRank(offer.energyEfficiencyClass)
  });

  protected readonly loadErrorTexts = { fallback: 'Failed to load offers', httpPrefix: 'Error loading offers' };

  protected fetchOffers(): Observable<FlatOffer[]> {
    return this.flatsService.getCurrentOffers();
  }
}
