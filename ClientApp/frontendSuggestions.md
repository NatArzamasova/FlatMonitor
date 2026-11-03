# Frontend styling suggestions

Notes from a review of the current Angular frontend (Angular 18, Bootstrap 5.3 CSS, Chart.js 4).
The app works but looks like plain Bootstrap: a big blue `display-4` title, dark table headers, native
multi-selects and `alert-info` boxes for every empty state. The suggestions below are ordered by
effort-to-impact. None of them needs a new UI framework. Bootstrap 5.3 can do all of it.

---

## 1. Quick wins (CSS / template only, < 1 h each)

### 1.1 Replace the header with a real app bar ✅ done
Implemented as the shared `AppHeaderComponent` (`src/app/shared/app-header/`), used by the dashboard
(with the tabs) and the presets page (with "Back to dashboard", no tabs).

`dashboard.component.html` uses an `h1.display-4` (blue, ~3.5rem) plus a separate tab row.
- Use a slim sticky `navbar` with the brand on the left ("FlatMonitor · Karlsruhe"), the three tabs
  as nav links in the middle, and "Configure filters" as an icon button on the right.
- Give the presets page the same bar, so the two pages look like one app.
- Remove the global `.display-4 { color: #1a73e8 }` override from `styles.scss`.

```html
<nav class="navbar navbar-expand bg-body border-bottom sticky-top px-3">
  <a class="navbar-brand fw-semibold" routerLink="/overview">🏠 FlatMonitor</a>
  <ul class="navbar-nav nav-underline gap-3">
    <li class="nav-item"><a class="nav-link" routerLink="/overview" routerLinkActive="active">Dashboard</a></li>
    …
  </ul>
  <a class="btn btn-sm btn-outline-secondary ms-auto" routerLink="/presets" target="_blank">Filters</a>
</nav>
```
`nav-underline` (Bootstrap 5.3) looks more modern than `nav-tabs`.

### 1.2 Lighter tables ✅ done
Implemented as `.table-card` (wrapper, replaces `.table-responsive`) and `.data-table` in `styles.scss`.
On the Dashboard and Current Offers tabs the table fills the window height left below the filters
(`.app-shell` / `.fill-column` / `.table-card-fill`) and scrolls inside its card, so its header stays visible.
This replaces `top: <navbar height>`, because sticky cells do not stick to the page inside a horizontally
scrolling wrapper. On the History tab the chart and the "Latest averages" table fill that height together
(`.history-layout`, from the lg breakpoint; stacked below it with a 360 px chart), and the chart follows
its size through a `ResizeObserver`. This also covers the "fixed 600 px chart height" point of 2.7.

- `thead.table-dark` is visually heavy. Use a light header (`table-light`, or a custom
  `--bs-table-bg` with uppercase, small, muted header text) and drop `table-striped`, keeping
  `table-hover`.
- Sticky header for the long offers table: `thead th { position: sticky; top: <navbar height>; }`.
- Right-aligned numbers should use `font-variant-numeric: tabular-nums;` so the digits line up.
- Wrap each table in a `card` (`card` + `card-body p-0`) instead of the custom
  `.table { box-shadow }` rule.
- `SortableHeaderComponent` uses `link-light`. With a light header switch it to `link-body-emphasis`
  and show a muted ↕ on unsorted columns, so it is clear that columns can be sorted.

### 1.3 Better price-drop display (offers tab) ✅ done
Later the "Original price" column was removed altogether: the price with its discount badge is enough,
and the badge's tooltip shows the original price ("Was €2,200 when first listed").

Right now a drop shows as `€4,200 (- 5%)` and the original price is **red** and struck through. Red reads
as "bad", but for a flat hunter a price drop is good news.
- Show the discount as a small green badge: `<span class="badge text-bg-success-subtle text-success-emphasis">−5 %</span>`.
- Show the original price muted (`text-body-secondary text-decoration-line-through`), not red.

### 1.4 Source column ✅ done
Implemented with `sourceBadge()` (`core/utils/source.utils.ts`) and the `.source-*` classes in `styles.scss`.
Offers without a usable URL show the pill without a link.

`View (Immowelt)` buttons make the table wide and repetitive. Use a small coloured pill per source
(e.g. IS24 orange, Immowelt yellow, ohne-makler green) plus an ↗ icon link. This also lets the user
see at a glance which portal a flat comes from.

### 1.5 Consistent filter labels ✅ done
All tabs now use "District:", "Rooms:", "Preset:" and a "Group by:" group with the options "District" /
"Rooms" (checkboxes on the Dashboard tab, radios on History). The chart title and the "Latest averages"
header say "Rooms" instead of "Room Number" / "Room count".

The overview says "Filter District:" / "Filter Rooms:", the other tabs say "District:" / "Rooms:", and
history says "Group By:" while overview uses checkboxes. Pick one wording ("District", "Rooms",
"Group by") and use it on every tab.

### 1.6 Clean up `styles.scss` ✅ done
The fixed colours are Bootstrap variables now (`body` uses `--bs-tertiary-bg`), the global `.text-muted`
size and the rules that repeated Bootstrap's defaults are gone, and `text-muted` (deprecated in 5.3) is
replaced by `text-body-secondary`. The chart height had already moved out of the inline style (see 1.2).
The source pill and chart colours are still fixed values; they are left for dark mode (3.2) and 2.7.

- `.text-muted { font-size: 0.9rem }` changes the size of *every* muted text, including inside
  Bootstrap components. Remove it and use `.small` where needed.
- `h1…h6 { color: #333 }` and `body { background: #f5f5f5 }` are fixed values that will break dark mode
  (see 3.2). Use Bootstrap CSS variables instead (`--bs-body-bg`, `--bs-emphasis-color`).
- Move the inline `style="height: 600px"` from `HistoryChartComponent` into a class.

### 1.7 Icons instead of emoji / text glyphs
`⚙`, `★`, `✓`, `+` are rendered differently on each OS. Add `bootstrap-icons` (an npm package with only a
font and CSS, no JS) and use `bi-gear`, `bi-star-fill`, `bi-arrow-clockwise`, `bi-box-arrow-up-right`, etc.

---

## 2. Medium effort (component changes)

### 2.1 Replace the native `<select multiple size="1">` ✅ done
`MultiSelectFilterComponent` is now a button ("All" / the one option / "3 selected") with a menu of
checkboxes, "Select all", "Clear", and a search box for lists with more than 8 options. Plain Angular with
Bootstrap's dropdown CSS (no Popper, no ng-bootstrap); closes on Escape and on a click outside.
Styled as a menu: whole-row options with a hover background, ticked rows tinted in the primary colour,
small rounded checkboxes, a divider under the search/Select all/Clear header with the selected count, a
shadow, and a tinted toggle button while the filter is active.

`MultiSelectFilterComponent` renders a one-line native multi-select. It is hard to use: you need
Ctrl+click, you cannot see what is selected, and it looks different in each browser. Replace it with a
Bootstrap dropdown with checkboxes:
- Button text summarises the choice: "All districts" / "Südstadt" / "3 districts".
- The menu has a search box (27 districts), "Select all" / "Clear", and checkboxes.
- The component API (`options`, `[(selected)]`, `changed`) stays the same, so the hosts do not change.

Bootstrap's dropdown needs Popper/JS. Toggling a `show` class from Angular plus a click-outside
listener is enough. Alternatively use `@ng-bootstrap/ng-bootstrap` (`NgbDropdown`).

### 2.2 Filter toolbar as a card + active-filter chips ✅ done
Implemented as the shared `FilterToolbarComponent` (`shared/filter-toolbar/`) with chips built by
`buildFilterChips()` (`core/utils/filter-chips.utils.ts`), on all three tabs. "Reset filters" also
deselects the preset. Below md the card collapses behind "Filters (n)"; the chips stay visible.

Put the filter rows of each tab into one `card` with a `card-body py-2` "toolbar". Below it, show
the active filters as removable chips (`Südstadt ✕`, `3 rooms ✕`, `≤ 400 000 € ✕`) and a
"Reset filters" link. On small screens collapse the toolbar behind a "Filters (3)" button.

### 2.3 KPI cards on the Dashboard tab ✅ done
Four tiles from the aggregations (`computeOverviewKpis()`, `StatTileComponent`): flats on offer (with the
number of districts), avg. €/m², cheapest and priciest district. They follow the filters, not the grouping.
The price-drop tile was left out: the Dashboard tab loads no single offers, so it would need an extra
request for the current offers.

Above the aggregation table, show 3–4 stat tiles computed from the data the tab already loads:
- Flats on offer (after filters)
- Average €/m² (after filters)
- Cheapest district by €/m²
- Flats with a price drop (count; links to the offers tab)

```html
<div class="row g-3 mb-4">
  <div class="col-6 col-lg-3">
    <div class="card h-100"><div class="card-body">
      <div class="text-body-secondary small text-uppercase">Avg. price / m²</div>
      <div class="fs-3 fw-semibold">4 250 €</div>
    </div></div>
  </div>
  …
</div>
```

### 2.4 Compact scraping-status bar ✅ done
Moved into the app header instead of the Dashboard tab: `ScrapeControlComponent`
(`features/dashboard/scrape-control/`) shows a status dot with "Last scrape 2 h ago · 246 flats" (details
panel on click), "Scrape now" / "Cancel", inline errors and a progress bar along the header's bottom edge
while a scrape runs. It replaces `ScrapingStatusComponent`. "Refresh Data" became a ↻ button in the header;
it and every finished scrape reload the open tab through `DataReloadService` (before, only the Dashboard
tab reloaded after a scrape). The empty states now point to "Scrape now" at the top.

`ScrapingStatusComponent` shows two paragraphs of text with badges. Turn it into one line:
a coloured status dot + "Last scrape 2 h ago · 246 flats" (with a tooltip for the exact time) and the
Refresh / Scrape buttons on the right. Relative times ("2 h ago") are easier to read than `short` dates.
During a scrape, show a thin progress bar (`progress` with `progress-bar-striped progress-bar-animated`)
below the navbar instead of only changing the button.

### 2.5 Empty and loading states ✅ done
`EmptyStateComponent` (icon, heading, text, actions) replaces the `alert-info` boxes on all tabs: "no data yet"
offers "Start a scrape" (`StartScrapeButtonComponent`), "filters match nothing" offers "Reset filters". After
a load error only the error shows. `LoadingSkeletonComponent` (toolbar + table or chart placeholders)
replaces `LoadingSpinnerComponent`, which is removed. The header and the start button share the scrape error
wording (`describeScrapeStartError()`).

- Replace `alert-info` empty states with a centred empty state: muted icon, one line of text,
  and a primary button ("Start a scrape"), instead of telling the user to click a button on another tab.
- Replace the full-page spinner with skeleton rows (`placeholder-glow` + `placeholder` in table
  cells). The layout then does not jump when the data arrives.

### 2.6 Toasts for success messages ✅ done
`ToastService` + `ToastContainerComponent` (in the app root, bottom right, hidden after 4 s, at most 4).
Toasts for: scrape started, cancel requested, scrape finished with its outcome (success / partly failed /
failed / canceled, noticed from the polled status); presets saved and created (filter bar and presets page),
set or unset as default, deleted. Errors stay inline.
"Scrape started" etc. currently push the page down as an alert. Use a Bootstrap toast in the corner
for success/info (auto-hide after 4 s) and keep inline alerts only for errors.

### 2.7 History chart polish (`history-chart.config.ts`) ✅ done
10-colour palette without yellow, light grey and black; hovering highlights the nearest line, dims the others
and shows a tooltip for that line only (not every line of the date); `tension: 0.2`; subtle horizontal
grid, no vertical grid, y ticks as `€4,200`. Colour dots in the "Latest averages" table replace the legend,
which is only shown (small, at the bottom) when a line has no point on the latest date. The height was done
with 1.2.

- **Palette:** `#F0E442` (yellow), `#BBBBBB` and `#DDCC77` have very low contrast on white, and `#000000`
  will disappear in dark mode. Drop them and keep 8–10 distinct colours. When more series are selected,
  highlight the hovered series and dim the others instead of using 28 colours.
- **Legend:** with many districts, the top legend takes half the chart. Move it to the right
  (`position: 'right'`) on wide screens or replace it with the "Latest averages" table next to the
  chart (add a colour dot per row, then the legend can be hidden).
- `tension: 0.4` makes the line curve beyond the real data points; use `0.2` or `0`.
- Use `interaction: { mode: 'index', intersect: false }` so one tooltip shows all series for a date.
- Subtle grid (`grid: { color: 'rgba(0,0,0,.06)' }`), no x grid, y ticks formatted as `4 200 €`.
- A fixed height of 600 px is too tall on laptops. Use `clamp(320px, 55vh, 600px)`.

### 2.8 Presets page ✅ done
The districts are toggle pills (`btn-check` + `btn-outline-primary rounded-pill`), deleting asks
inline ("Delete preset "East"? Delete / Keep") instead of `window.confirm`, and the "Saved ✓" confirmation is
done through the toasts of 2.6.
- Show districts as toggle pills (`btn-check` + `btn btn-sm btn-outline-primary`) instead of
  three columns of checkboxes. This is quicker to scan and to click.
- Ask before deleting (small modal or a second "Really delete?" click).
- Show a "Saved ✓" confirmation after Save.

---

## 3. Bigger changes (one story each)

Each item below is written as a story: what it is for, where the code stands today, what to do, and when
it is done. Sizes: S = up to half a day, M = about a day, L = several days.

### 3.1 Story: Theme the app through Bootstrap's Sass variables ✅ done
`styles.scss` sets the theme variables and imports `bootstrap/scss/bootstrap`; `angular.json` no longer loads
`bootstrap.min.css`. Inter comes from `@fontsource/inter` (400/500/600). The heading, button and badge weights,
the badge padding and the alert radius are Sass variables now. `$card-border-color` is
`rgba(var(--bs-emphasis-color-rgb), .08)` instead of a fixed black, so it also works in dark mode, and `.table-card`
uses it too. No silencing of Sass deprecations needed: the installed Sass (1.77) does not warn about `@import` yet.
The history chart still uses Chart.js' own font; 3.2 reads the chart's styling from the CSS variables anyway.

**Story.** As a user of FlatMonitor, I want one consistent look (brand colour, font, corner radius, card
borders) on every page, so that the app feels like one product and not like plain Bootstrap with patches.

**Today.**
- `angular.json` loads the compiled `node_modules/bootstrap/dist/css/bootstrap.min.css` (build *and* test
  config), then `src/styles.scss` overrides parts of it (`.btn` / `.badge` font weight, `.alert` radius,
  `.data-table` header, …).
- Components use Bootstrap's CSS variables (`--bs-primary-*`, `--bs-tertiary-bg`, `--bs-border-radius-lg`),
  so a theme change through the variables reaches them without touching each component.
- The font is the system font stack (Bootstrap default).

**Tasks.**
1. Remove `bootstrap.min.css` from both `styles` arrays in `angular.json`.
2. At the top of `src/styles.scss`, set the theme variables and import Bootstrap's Sass:
   ```scss
   $primary: #2f6fde;
   $font-family-sans-serif: 'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
   $border-radius: .5rem;
   $border-radius-lg: .75rem;
   $card-border-color: rgba(0, 0, 0, .06);
   $enable-shadows: false;
   @import 'bootstrap/scss/bootstrap';
   ```
   (Bootstrap 5.3 still uses `@import`; the Sass deprecation warnings it causes can be silenced with
   `"stylePreprocessorOptions": { "sass": { "silenceDeprecations": ["import"] } }` if they are noisy.)
3. Add the font as the self-hosted npm package `@fontsource/inter` (weights 400, 500, 600) and import it in
   `styles.scss`, so the app sends no requests to Google.
4. Move the overrides that are only theme values into variables (`$btn-font-weight: 500`,
   `$badge-font-weight: 500`, `$alert-border-radius: $border-radius-lg`) and delete them from the CSS part.
5. Check the screens: header, filter toolbar and dropdown, chips, tiles, tables, empty states, toasts,
   presets page.

**Acceptance criteria.**
- `angular.json` no longer references `bootstrap.min.css`; Bootstrap is compiled from Sass.
- The primary colour, font and radii are set in one place in `styles.scss`; no component repeats them.
- The Inter font is loaded from the app bundle, not from a CDN.
- `ng build` and `ng test` pass; the production bundle stays within the existing budgets.
- Apart from the new colour, font and radius, the pages look as before (no lost spacing or layout).

**Tests.** No new unit tests (pure styling). Existing specs must stay green; check the pages by hand.

**Dependencies / risks.** Builds on 1.6 (done). Should come before 3.2, so dark mode is designed on the
final theme. Risk: a few Bootstrap class combinations may look different after compiling from Sass with new
values; the manual check (task 5) covers it.

**Size.** M.

### 3.2 Story: Dark mode ✅ done
`ThemeService` (`core/services/theme.service.ts`) stores `system | light | dark` under `flatmonitor.theme`, sets
`data-bs-theme` on `<html>` and follows `prefers-color-scheme` live in system mode. An inline script in `index.html`
sets the theme before the styles load (no white flash). `ThemeToggleComponent` sits at the end of the app header on
both pages (inline SVG icons, since 1.7 is not done; `aria-label` "Theme: System (switch to Light)"). The source pills
have dark variants (contrast ≥ 8:1). The chart takes a `ChartTheme` (text and grid colour, font, dark flag) read
from Bootstrap's CSS variables by `readChartTheme()` and is rebuilt on a theme change; it now also uses Inter. The
dark palette lightens the four colours below 3:1 on the dark background (`#0072B2`, `#332288`, `#117733`,
`#882255`); the "Latest averages" dots switch with it.

**Story.** As a user who works in the evening, I want a dark theme that follows my system setting and that I
can switch myself, so that the dashboard is easy on the eyes and fits my other apps.

**Today.**
- Bootstrap 5.3 supports `data-bs-theme="dark"` on `<html>`; almost all colours in the app are already
  Bootstrap variables (1.6), so most of the UI switches with it.
- Still fixed and unreadable or wrong in dark mode:
  - the source pills (`.source-is24`, `.source-immowelt`, `.source-ohne-makler` in `styles.scss`: light
    backgrounds with dark text);
  - the history chart: `GRID_COLOR = 'rgba(0, 0, 0, 0.06)'`, Chart.js' default grey text for ticks, title and
    legend, and the line palette, which is chosen for a white background;
  - the `body` background `--bs-tertiary-bg` and the header's `bg-body` switch on their own.
- No stored user preferences yet (no `localStorage` use in the app).

**Tasks.**
1. `ThemeService` (core/services): the choice `'light' | 'dark' | 'system'`, stored in `localStorage`
   (default `system`); sets `data-bs-theme` on `document.documentElement`; for `system` it follows
   `matchMedia('(prefers-color-scheme: dark)')` and its `change` event. Wrap `localStorage` access in
   `try/catch` (private windows).
2. Set the theme before Angular starts (a small inline script in `index.html` reading the stored choice), so
   the page does not flash white on load.
3. A theme toggle in the app header (on the dashboard and the presets page): an icon button cycling
   System → Light → Dark, with an `aria-label` that names the current choice.
4. Source pills: add `[data-bs-theme="dark"]` variants of the three `--source-bg` / `--source-fg` pairs
   (dark tinted background, light text).
5. Chart: read text and grid colours from the CSS variables when the chart is built
   (`getComputedStyle(document.documentElement).getPropertyValue('--bs-secondary-color')`,
   `--bs-border-color-translucent`) and rebuild the chart when the theme changes
   (`HistoryChartComponent` subscribes to `ThemeService`). Check the 10 line colours on the dark background
   and lighten the two darkest (`#332288`, `#882255`) for dark mode if they do not stand out.
6. Check every screen in both themes, including toasts, dropdowns, empty states and the skeleton.

**Acceptance criteria.**
- On first visit the app follows the system setting; switching the system setting switches the app live.
- The header toggle switches between System, Light and Dark; the choice survives a reload.
- No white flash when loading in dark mode.
- All text has at least WCAG AA contrast in both themes, including source pills, chart axes and legend.
- The chart switches colours without a page reload.

**Tests.**
- `ThemeService`: default `system`; stores and restores the choice; sets `data-bs-theme`; follows the media
  query in `system` mode; works when `localStorage` throws.
- Header: the toggle cycles the three modes and updates its `aria-label`.
- Chart config: takes the text/grid colours from its input instead of the fixed constants.

**Dependencies / risks.** After 3.1 (final theme). The chart is the most work, because Chart.js does not
read CSS variables itself.

**Size.** M–L.

### 3.3 Story: German number, currency and date formats ✅ done (option A)
`core/utils/locale.utils.ts` holds `APP_LOCALE` (`de-DE`), `formatEuro()`, `formatDecimal()` and
`provideAppLocale()` (`registerLocaleData`, `LOCALE_ID`, `DEFAULT_CURRENCY_CODE`), used in `main.ts` and by the specs
that check formatted text. The chart (ticks, tooltip, x-axis dates `15. Jan.`), the filter chips, the
multi-select's number options (`2,5`) and the History tab's "2,5 rooms" labels use them; no `Intl` formatter
hard-codes a locale anymore. The templates keep `currency: 'EUR'` because they pass the digits argument.
Open point from task 4: the price inputs are `type="number"`, which reads a dot as a decimal point, so typing
`200.000` filters at 200 €, not 200 000 €. `200000` works. Fixing it means a text input with German parsing.

**Story.** As a user in Karlsruhe, I want prices, areas and dates in the format I am used to (`4.200 €`,
`65,5 m²`, `15.01.2024`), so that I can read the figures at a glance.

**Today.** Everything is formatted in US English:
- Angular pipes without a locale (`currency: 'EUR'`, `number`, `date`) in the Dashboard, Current Offers and
  History templates and in the scrape control → `€4,200`, `2.5`, `1/15/24, 10:00 AM`;
- fixed `Intl` formatters: `en-US` for prices in the chart ticks and the filter chips
  (`filter-chips.utils.ts`), `en-GB` for the chart's x-axis dates;
- many specs assert these texts (e.g. `'€3,750'`, `'from €200,000'`, `'Was €2,200 when first listed'`).

**Decision needed first.** The UI text is English. Options:
- **A. German formats, English text** (`de-DE` locale): "Avg. price / m² 4.250 €". What German users
  expect from the numbers, but mixes languages.
- **B. Keep English formats** and close this story.
- **C. Translate the whole UI to German** as well (Angular i18n): consistent, but a much bigger story of its
  own.

Recommendation: A. Numbers are what the user reads most; the English labels are short and stay clear.

**Tasks (option A).**
1. In `main.ts`: `registerLocaleData(localeDe)` and `{ provide: LOCALE_ID, useValue: 'de-DE' }`; set the
   default currency (`DEFAULT_CURRENCY_CODE` = `'EUR'`) so templates can use `currency` without arguments.
2. Replace the fixed `Intl` locales with one shared `APP_LOCALE` constant (core/utils) used by the chart
   (`history-chart.config.ts`), the filter chips and `formatTimeAgo` if it ever uses `Intl`.
3. Check the date formats: `date: 'short'` / `'medium'` give German dates with `de-DE`; the chart's x-axis
   becomes `15. Jan.`.
4. Number inputs (price min/max on Current Offers) keep working with the browser's own locale; check that
   `200000` and `200.000` behave as expected.
5. Update the specs' expected texts to the German formats (search for `€`).

**Acceptance criteria.**
- All prices show as `4.200 €`, decimals with a comma (`2,5 rooms`, `65,5 m²`), dates as `15.01.24, 10:00`
  or `15.01.2024, 10:00:00`, the chart axis as `4.200 €` and `15. Jan.`.
- No formatter in the app hard-codes a locale anymore; changing `LOCALE_ID` / `APP_LOCALE` switches all.
- `ng test` passes with the updated expectations.

**Tests.** Update the existing expectations; add one test per shared formatter (chart ticks, chips) that
it follows `APP_LOCALE`.

**Dependencies / risks.** Independent of the other stories. Risk: specs assert many formatted strings, so
the change touches many spec files (mechanical).

**Size.** S–M (option A), after the decision.

### 3.4 Story: Offers as cards on small screens ✅ done
`OfferCardListComponent` (`offers-tab/offer-card-list/`) renders `offerRows` as cards below md (CSS only:
`d-md-none` on the list, `d-none d-md-block` on the table, both in the DOM). Each card shows "Südstadt · 3 rooms", the
price with the price-drop pill, "65,5 m² · 4.250 € / m²" and the source pill with the same link markup as the table.
The "Sort by" select and the ↑/↓ button emit the same `sortRequested` column as the header buttons (a new column
sorts ascending, the button asks for the current one again, which toggles). `OfferRow`, `OfferSortColumn` and the
column list moved to `offers-tab/offer-row.ts`. The list scrolls inside `.card-list-fill`, below the sort control;
`LoadingSkeletonComponent` got `cardsOnSmallScreens` (card placeholders below md).

**Story.** As a user checking new flats on my phone, I want each offer as a readable card instead of a
table that scrolls sideways, so that I can scan price, size and district without zooming.

**Today.**
- Current Offers is a 6-column table (District, Rooms, Price, Living area, Price/m², Source) in a
  `.table-card` that scrolls horizontally on narrow screens and fills the window height
  (`.table-card-fill`).
- Sorting is done through the header buttons (`SortableHeaderComponent`), which a card list does not have.
- Below `md` the filter toolbar already collapses behind "Filters (n)" (2.2).

**Tasks.**
1. Below `md` (768 px), render `offerRows` as a list of cards instead of the table (CSS `d-md-none` /
   `d-none d-md-block`, or one `@if` on a `BreakpointObserver` signal so only one is in the DOM):
   - title: district and rooms ("Südstadt · 3 rooms");
   - price large, with the green price-drop pill (1.3);
   - secondary line: living area and €/m²;
   - the source pill with the link to the offer (1.4).
2. A sort control for the cards: a small `<select>` "Sort by" with the same columns and a direction toggle,
   driving the existing `TableSort` (`onSort`).
3. The card list scrolls inside the remaining height like the table (reuse `.table-card-fill` or a
   `.card-list-fill`), so the layout stays the same.
4. The empty states and the skeleton (2.5) work unchanged; give the skeleton a card variant for small
   screens.

**Acceptance criteria.**
- Below 768 px no horizontal scrolling on Current Offers; every offer is a card with all six values.
- Sorting works on small screens through the sort control, with the same order as the table.
- From 768 px the table is unchanged.
- Cards are keyboard-reachable; the offer link has the same accessible name as in the table.

**Tests.**
- Offers tab: renders cards with district, rooms, price, discount pill, area, €/m², source link (test the
  card template directly, independent of the screen width).
- The sort control calls the same sorting as the header buttons.

**Dependencies / risks.** None. If both table and cards stay in the DOM (pure CSS), the existing table specs
keep working unchanged, but the DOM doubles; a `BreakpointObserver` (`@angular/cdk/layout`, new package)
avoids that. Recommendation: CSS first (simpler), `BreakpointObserver` only if the list gets slow.

**Size.** M.

### 3.5 Story: Make the clickable Dashboard rows look clickable

**Story.** As a user of the Dashboard tab, I want to see that a row of the aggregation table opens the
matching offers, so that I discover the shortcut and can use it with the keyboard.

**Today.**
- Each row in the Dashboard table has `(click)` and `(keydown.enter)` → `openOffersForRow()` (opens Current
  Offers with the row's district and rooms in a new browser tab), `tabindex="0"`, `role="button"` and the
  title "Open matching current offers".
- Nothing shows this: the cursor is the default arrow, there is no visual hint, and the focus outline is the
  browser's default on a table row (barely visible).
- `role="button"` on a `<tr>` removes the row's table semantics for screen readers.

**Tasks.**
1. A `.row-link` class on these rows (styles.scss): `cursor: pointer`, the existing hover background, and a
   clear `:focus-visible` outline (`outline: 2px solid var(--bs-primary); outline-offset: -2px`).
2. A last, narrow column with a muted `›` (or the icon from 1.7) that turns primary on row hover.
3. Accessibility: drop `role="button"` from the `<tr>`; instead make the chevron cell contain a real link
   (`<a>` to the offers URL, `target="_blank"`, accessible name "Open offers in Südstadt with 3 rooms"),
   keep the row click as a mouse shortcut. Ctrl/middle click and "copy link" then work too, like
   "Configure filters".
4. Space should not scroll the page when the row is focused (only relevant if the row keeps a tabindex).

**Acceptance criteria.**
- Hovering a row shows a pointer and highlights the row and its chevron.
- Tabbing reaches one link per row with a clearly visible focus outline; Enter opens the offers.
- The link's accessible name says which offers it opens.
- Clicking anywhere on the row still opens the offers in a new browser tab.
- The table keeps its row/cell semantics (no `role="button"` on rows).

**Tests.**
- Overview tab: each row has a link with the offers URL (district and rooms query parameters, matching the
  grouping), `target="_blank"`, `rel="noopener"` and the accessible name; the row click still opens it
  (existing `openOffersForRow` tests).
- Grouping off: the link leaves out district/rooms like today.

**Dependencies / risks.** None; uses the chevron icon from 1.7 if that is done first.

**Size.** S.

---

## Status and suggested order

Done: 1.1–1.6 and 2.1–2.8.

Open, in this order:
1. **1.7 Icons** (S): small, and 3.2's theme toggle and 3.5's chevron can use the icons.
2. **3.5 Clickable rows** (S): quick, improves discoverability and accessibility.
3. ~~**3.1 Sass theme** (M): the base for dark mode.~~ ✅ done
4. ~~**3.2 Dark mode** (M–L): after 3.1.~~ ✅ done
5. ~~**3.3 German formats** (S–M): once the format decision is made; independent of the others.~~ ✅ done
6. ~~**3.4 Offers as cards on small screens** (M): when the app is used on phones.~~ ✅ done

Specs that query by CSS class or element must be updated together with the change (e.g. 3.3 changes
many expected texts, 3.5 the row markup).
