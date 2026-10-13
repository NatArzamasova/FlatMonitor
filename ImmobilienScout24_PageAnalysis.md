# ImmobilienScout24 Page Structure Analysis

## Overview
Successfully logged into immobilienscout24.de and extracted page HTML (1.4MB). Page uses React with data-testid attributes for selectors.

## Key Findings

### Listings Container
- **Selector**: `data-testid="ListingsGrid"` — main grid holding all listings
- **Card Container**: `class="touchpoint-card"` — individual listing wrapper
- **Card Type**: `data-testid="touchpoint-hs"` / `data-testid="touchpoint-card"`

### Listing ID & URL
- **Detail Page URL**: `/expose/{ID}` (relative URL, e.g., `/expose/164855556`)
- **ID Extraction**: Via `href="/expose/(\d+)"` or `data-exp-id="(\d+)"`
- **Pattern**: Numeric ID unique per listing

### Data Fields Observed
- `data-testid="headline"` — listing title/description
- `data-testid="bottomBox"` — likely contains price and key attributes
- `data-testid="attributes"` — additional listing attributes (rooms, size, location)
- Multiple photo slides: `data-testid="{ID}-slide-{N}"` (image carousel)

### Pagination
- **Selector**: `data-testid="pagination-button"`, `data-testid="pagination-button-next"`, `data-testid="pagination-button-prev"`
- **Pattern**: Standard pagination buttons for navigating through pages
- **URL Pattern**: Query parameters (likely `page`, `pageSize`, or similar)

### Other Notable Elements
- **Sponsored listings**: `data-testid="dsa-link"` with "Gesponsert" label
- **Map view**: `data-testid="cxp-map--container"` (optional)
- **Filters**: Header controls for search refinement
- **Sorting**: `data-testid="sortingControlButton"`

## Next Steps

1. **Extract listing cards**: Query all `touchpoint-card` divs
2. **Parse each card**:
   - Extract ID from `href="/expose/(\d+)"`
   - Extract headline from `data-testid="headline"`
   - Extract price/attributes from `data-testid="bottomBox"` and `data-testid="attributes"`
   - Extract address from nested address selector (need to determine exact selector)
   - Extract size (m²) from attributes
   - Extract rooms/property type from attributes
3. **Handle pagination**: Detect next page button, construct next page URL
4. **Detail page parsing** (later phase): Navigate to `/expose/{ID}` and extract full details

## Selectors Summary

```
Listing Grid:           data-testid="ListingsGrid"
Listing Card:           class="touchpoint-card"
Card Content:           data-testid="touchpoint-hs"
Listing Title:          data-testid="headline"
Price/Attributes:       data-testid="bottomBox"
Attributes Section:     data-testid="attributes"
Detail Link:            href="/expose/(\d+)"
Pagination Next:        data-testid="pagination-button-next"
Pagination Prev:        data-testid="pagination-button-prev"
```

## Notes
- Page is JavaScript-heavy (React rendered)
- Uses semantic data-testid attributes for reliable selectors
- No visible postcode filter constraints on current page
- Listing ID appears to be immobilienscout24.de's own internal ID
- Photos are in carousel format (multiple slides per listing)
