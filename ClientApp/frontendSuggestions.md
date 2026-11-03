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

### 2.1 Replace the native `<select multiple size="1">`
`MultiSelectFilterComponent` renders a one-line native multi-select. It is hard to use: you need
Ctrl+click, you cannot see what is selected, and it looks different in each browser. Replace it with a
Bootstrap dropdown with checkboxes:
- Button text summarises the choice: "All districts" / "Südstadt" / "3 districts".
- The menu has a search box (27 districts), "Select all" / "Clear", and checkboxes.
- The component API (`options`, `[(selected)]`, `changed`) stays the same, so the hosts do not change.

Bootstrap's dropdown needs Popper/JS. Toggling a `show` class from Angular plus a click-outside
listener is enough. Alternatively use `@ng-bootstrap/ng-bootstrap` (`NgbDropdown`).

### 2.2 Filter toolbar as a card + active-filter chips
Put the filter rows of each tab into one `card` with a `card-body py-2` "toolbar". Below it, show
the active filters as removable chips (`Südstadt ✕`, `3 rooms ✕`, `≤ 400 000 € ✕`) and a
"Reset filters" link. On small screens collapse the toolbar behind a "Filters (3)" button.

### 2.3 KPI cards on the Dashboard tab
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

### 2.4 Compact scraping-status bar
`ScrapingStatusComponent` shows two paragraphs of text with badges. Turn it into one line:
a coloured status dot + "Last scrape 2 h ago · 246 flats" (with a tooltip for the exact time) and the
Refresh / Scrape buttons on the right. Relative times ("2 h ago") are easier to read than `short` dates.
During a scrape, show a thin progress bar (`progress` with `progress-bar-striped progress-bar-animated`)
below the navbar instead of only changing the button.

### 2.5 Empty and loading states
- Replace `alert-info` empty states with a centred empty state: muted icon, one line of text,
  and a primary button ("Start a scrape"), instead of telling the user to click a button on another tab.
- Replace the full-page spinner with skeleton rows (`placeholder-glow` + `placeholder` in table
  cells). The layout then does not jump when the data arrives.

### 2.6 Toasts for success messages
"Scrape started" etc. currently push the page down as an alert. Use a Bootstrap toast in the corner
for success/info (auto-hide after 4 s) and keep inline alerts only for errors.

### 2.7 History chart polish (`history-chart.config.ts`)
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

### 2.8 Presets page
- Show districts as toggle pills (`btn-check` + `btn btn-sm btn-outline-primary`) instead of
  three columns of checkboxes. This is quicker to scan and to click.
- Ask before deleting (small modal or a second "Really delete?" click).
- Show a "Saved ✓" confirmation after Save.

---

## 3. Bigger changes (worth a story each)

### 3.1 Theme via Bootstrap Sass instead of CSS overrides
`angular.json` loads `bootstrap.min.css` and then overrides it in `styles.scss`. Import Bootstrap's
Sass in `styles.scss` and set the variables once:

```scss
$primary: #2f6fde;
$font-family-sans-serif: 'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
$border-radius: .5rem;
$card-border-color: rgba(0,0,0,.06);
$enable-shadows: false;
@import 'bootstrap/scss/bootstrap';
```
This gives one consistent brand colour, font and radius everywhere, and removes most of the custom rules.
For the font, use the self-hosted `@fontsource/inter` npm package (no requests to Google from the
app).

### 3.2 Dark mode
Bootstrap 5.3 supports `data-bs-theme="dark"`. Add a toggle in the navbar (default: follow
`prefers-color-scheme`, remember the choice in `localStorage`). This needs 1.6 done first (no fixed colours)
and the chart to read its text/grid colours from CSS variables.

### 3.3 German number and currency formatting
The app shows `€4,200` (en-US). Users of a Karlsruhe flat monitor expect `4.200 €`. Register the `de`
locale (`registerLocaleData(localeDe)` + `{ provide: LOCALE_ID, useValue: 'de-DE' }`) and use it in the
chart's `Intl` formatters too. If the UI language stays English, `en-DE` is not available, so this is a
decision between consistent English and German number formats. To decide.

### 3.4 Mobile layout for the offers table
Seven columns do not fit on a phone. Below `md`, show each offer as a card (district + rooms as
title, price large, €/m² and area as secondary text, source pill + link), above `md` keep the table.

### 3.5 Clickable rows: make it obvious
Overview rows open the offers tab, but nothing looks clickable. Add `cursor: pointer`, a
chevron `›` in the last column, and a visible `:focus-visible` outline (the rows have `tabindex="0"`).

---

## Suggested order

1. 1.6 + 3.1 (clean base: Sass theme, no fixed colours)
2. 1.1, 1.2, 1.3, 1.4, 1.5, 1.7 (header, tables, prices, icons)
3. 2.1 + 2.2 (filters: the biggest UX gain)
4. 2.3, 2.4, 2.5, 2.6 (dashboard feel)
5. 2.7, 2.8, 3.5
6. 3.2, 3.3, 3.4 when needed

Each step changes how things look, not how they behave. Specs that query by CSS class or element
(e.g. `select[multiple]` in the filter tests) must be updated together with 2.1 and 2.8.
