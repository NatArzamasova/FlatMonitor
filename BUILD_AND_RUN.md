# Building and Running FlatMonitor

## Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm
- Google Chrome, for the Immowelt source (see [Immowelt and Chrome](#immowelt-and-chrome))
- Environment variables in `.env` (for scraping credentials)

## Development Setup

### Run the backend and frontend separately

Terminal 1 - Backend:
```bash
dotnet run
```

Terminal 2 - Frontend (with live reload):
```bash
cd ClientApp
npm install # if this is the first run
npm start
```

The backend exposes the API on `http://localhost:5000`. The Angular dev server runs on `http://localhost:4200` and proxies API calls to the backend. Open `http://localhost:4200`; the backend does not serve frontend files.

### Option 3: CLI Mode (for scheduled scraping)

Run scraper without web server:
```bash
dotnet run -- scrape
```

This mode:
- Executes immediately and exits
- Writes new snapshots to `data/snapshots/`
- Useful for CI/CD pipelines

Scrape only some sources (comma-separated, any case); the other sources keep their flats, and the aggregations are
computed over all flats afterwards:
```bash
dotnet run -- scrape --source Immowelt
dotnet run -- scrape --source OhneMakler,ImmobilienScout24
```
The sources are `OhneMakler`, `ImmobilienScout24` and `Immowelt`, scraped in that order. An unknown name stops with
the list of valid ones.

Immowelt alone takes about 25 minutes (a pause of 5 s between its ~260 pages: ~9 results pages, one expose per
flat). While a scrape runs, Windows is kept from going to sleep (see [Immowelt and Chrome](#immowelt-and-chrome)).

Exploring a site's pages for parser work (not part of a scrape):
```bash
dotnet run -- explore-is24             # the first listings page, saved to the temp directory
dotnet run -- explore-immowelt --cdp   # two results pages and an expose in the debug Chrome, saved to data/explore/Immowelt/
```

### Immowelt and Chrome

immowelt.de is guarded by DataDome, which blocks `HttpClient` and also Playwright's own browser (even with a window).
It lets through a normal Chrome, so the Immowelt scraper works in a tab of the installed Chrome, connected over the
Chrome DevTools Protocol (CDP) at `http://127.0.0.1:9222`:

- **No Chrome there:** the scraper starts `chrome.exe` itself with the debugging port and its own profile
  (`%LOCALAPPDATA%\FlatMonitorChrome`), and closes it after the run. A window opening during a scrape is expected.
- **Chrome already there** (started by hand, see below): the scraper opens its own tab, closes only that tab, and
  leaves the browser open.
- **Robot check:** passed once in that profile, it is remembered in its cookies. If DataDome shows it again, the run
  fails with "blocked by DataDome"; start Chrome by hand, pass the check on immowelt.de, and scrape again.

Starting the debug Chrome by hand (PowerShell); recent Chrome refuses remote debugging on the default profile, hence
the own one:
```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222 --user-data-dir="$env:LOCALAPPDATA\FlatMonitorChrome"
```

**Windows sleep:** in Modern Standby, Windows cut the scraper's connection to Chrome mid-run. A scrape therefore holds
a Windows power request for its whole run (every source, CLI and `POST /api/scrape`); it shows in `powercfg /requests`
(admin terminal) as "FlatMonitor scrape run N". The screen still turns off and locks as usual. On battery, some laptops
sleep anyway: scrape plugged in. Should the connection still be lost, the remaining Immowelt flats keep their list
data (no details) and the run continues.

## Configuration

`FlatMonitor/appsettings.json` holds the settings shared by the web server and the CLI:

| Setting | Default | Meaning |
|---|---|---|
| `ConnectionStrings:Flats` | `Data Source=data/listings.db` | SQLite database |
| `Storage:DataDirectory` | `data` | JSONL and HTML snapshots |
| `Logging:LogLevel` | FlatMonitor `Information`, `Microsoft`/`System` `Warning` | Console log levels |
| `Scrapers:ImmobilienScout24:Headless` | `false` | Run the IS24 browser without a window (the robot guard may need a person) |
| `Scrapers:ImmobilienScout24:SearchUrl` | Karlsruhe flats for sale | First IS24 listings page; pagination starts there |
| `Scrapers:ImmobilienScout24:SaveDebugPages` | `false` | Save the first IS24 page to the temp directory when it has no listings (e.g. the robot guard); the error names the file |
| `Scrapers:Sources` | (empty: all) | Sources a scrape runs, comma-separated; `scrape --source` sets it on the CLI. E.g. `Scrapers__Sources=OhneMakler,ImmobilienScout24` keeps `POST /api/scrape` off Immowelt (a scrape can also choose among these per run) |
| `Scrapers:Immowelt:CdpUrl` | `http://127.0.0.1:9222` | Where the debug Chrome listens (`127.0.0.1`, not `localhost`: Chrome listens on IPv4 only) |
| `Scrapers:Immowelt:LaunchChrome` | `true` | Start Chrome if none listens at `CdpUrl`, and close it after the run |
| `Scrapers:Immowelt:ChromePath` | (empty: found) | `chrome.exe` to start; empty looks in `Program Files`, `Program Files (x86)` and `%LOCALAPPDATA%\Google\Chrome\Application` |
| `Scrapers:Immowelt:ChromeUserDataDir` | `%LOCALAPPDATA%\FlatMonitorChrome` | Profile of the started Chrome (keeps the passed robot check) |
| `Scrapers:Immowelt:SearchUrl` | Karlsruhe flats to buy, no auctions | First results page; pages 2.. are opened by URL (`&page=N`) |
| `Scrapers:Immowelt:PauseBetweenPagesMs` | `5000` | Pause between two pages (results pages and exposes); faster looks like a robot to DataDome. 5 s passed a full run; 3 s got the exposes blocked |
| `Scrapers:Immowelt:RobotCheckWaitSeconds` | `600` | When DataDome shows its robot check (results page or expose), the scraper brings its tab to the front and waits this long for you to pass it, then goes on. `0`: no waiting |
| `Scrapers:Immowelt:SaveDebugPages` | `false` | Save a page without data (e.g. the DataDome guard) to the temp directory; the error names the file |

Relative paths are resolved against the content root, which is the `FlatMonitor/` project directory: `dotnet run`
always runs there (`RunWorkingDirectory` in `FlatMonitor.csproj`), from any directory. When starting the built app
directly (`dotnet FlatMonitor.dll`), start it from `FlatMonitor/` or override the settings with environment variables,
e.g. `ConnectionStrings__Flats="Data Source=C:/flats/listings.db"` and `Storage__DataDirectory=C:/flats`.
`dotnet ef` reads the same file (run it from `FlatMonitor/` or with `--project FlatMonitor`).

## Project Structure

```
FlatMonitor/               # .NET Backend
├── Api/Controllers/       # REST API endpoints
├── Api/Contracts/        # API request and response contracts
├── Models/               # Domain models (Flat, PublishedFlat/StagedFlat rows, DailyFlatAggregation)
├── Storage/              # Database context and migrations
├── Scraping/             # Source-specific scrapers
├── data/                 # Data folder (snapshots, database)
└── Program.cs            # Dual-mode entry point

ClientApp/                # Angular Frontend
├── src/
│   ├── app/
│   │   ├── core/         # Services, models
│   │   ├── features/     # Feature modules (dashboard)
│   │   ├── app.component.ts
│   │   └── app.routes.ts
│   └── index.html
├── angular.json          # Angular CLI config
├── package.json          # Dependencies
└── proxy.conf.json       # Dev server proxy config
```

## API Endpoints

Every response body is an `ApiResponse` envelope: `{ "success": bool, "data": ..., "message": string }`.
Unexpected errors return `500` with `success: false` and the generic message "An unexpected error occurred";
the details are only in the server log.

- `GET /api/flats/aggregated` — Returns aggregated flat statistics
- `POST /api/scrape` — Queues a manual scrape (`202`, `data` = job id and queue time); `409` while one is queued or running.
  Optional body `{ "sources": ["OhneMakler", "ImmobilienScout24"] }` scrapes only those (any case; `400` for none or an
  unknown one); the sources left out keep their flats. "Scrape now" in the header has a menu to choose them
- `GET /api/scrape/sources` — The sources a scrape can run (those of `Scrapers:Sources`)
- `GET /api/scrape/status` — Whether a scrape is in progress, plus the last and last successful run (with the `sources` it
  scraped; `null` for runs from before they were recorded)
- `POST /api/scrape/cancel` — Cancels the queued or running scrape (`data` = whether there was one)
- `GET /swagger` — Swagger UI (development only)

## CI Pipelines

Every push runs a pipeline on the platform it was pushed to: `.gitlab-ci.yml` on GitLab (`origin`),
`.github/workflows/ci.yml` on GitHub (`GitHubFlatMonitor`). Both run the same seven jobs:

| Job | Script | Checks |
|---|---|---|
| commit-messages | `scripts/check-commit-msg.sh --range <from>..<to>` | every pushed commit matches `type(scope): message` |
| backend-build | `scripts/ci/backend-build.sh` | `dotnet restore` + `dotnet build -c Release` |
| backend-test | `scripts/ci/backend-test.sh` | NUnit tests from the build's output, JUnit report; collects raw coverage |
| backend-coverage | `scripts/ci/backend-coverage.sh` | coverage report from backend-test's raw coverage (no tests run) |
| frontend-build | `scripts/ci/frontend-build.sh` | `ng build` (production) |
| frontend-test | `scripts/ci/frontend-test.sh` | Karma/Jasmine specs in headless Chrome, JUnit report; collects raw coverage |
| frontend-coverage | `scripts/ci/frontend-coverage.sh` | coverage report from frontend-test's raw coverage (no tests run) |

The test jobs start once their build job is green, the coverage jobs once their test job is. Failed tests are listed by name in GitLab's pipeline "Tests" tab /
the "Backend tests" and "Frontend tests" check runs on GitHub.

**Code coverage:** the test jobs collect it while the tests run (raw Cobertura in their artifact); the coverage jobs
turn it into reports with ReportGenerator (local tool in `dotnet-tools.json`, `scripts/ci/coverage-report.sh`). It does
not fail a job.

| | Backend | Frontend |
|---|---|---|
| Collected by | coverlet (`FlatMonitor.Tests/coverage.runsettings`: the `FlatMonitor` assembly, without EF migrations) | karma-coverage (`ClientApp/karma.conf.js`) |
| Raw coverage (test job artifact) | `test-results/coverage-raw/<guid>/coverage.cobertura.xml` | `ClientApp/test-results/coverage-raw/cobertura.xml` |
| Full report (coverage job artifact) | `test-results/coverage/backend/index.html` | `test-results/coverage/frontend/index.html` |
| GitLab | line coverage on the coverage job, the pipeline and the MR (`coverage:` regex on the log); covered/uncovered lines in the MR diff (Cobertura report) | the same |
| GitHub | coverage table in the coverage job's summary (`SummaryGithub.md`) | the same |

Locally, the same scripts (below) write the same reports, e.g. open `test-results/coverage/backend/index.html`.

**Reproduce a job locally** (Git Bash, from the repository root):
```bash
sh scripts/ci/backend-build.sh && sh scripts/ci/backend-test.sh && sh scripts/ci/backend-coverage.sh
sh scripts/ci/frontend-build.sh && sh scripts/ci/frontend-test.sh && sh scripts/ci/frontend-coverage.sh
sh scripts/check-commit-msg.sh --range origin/main..HEAD
```
Locally the frontend scripts reuse `ClientApp/node_modules` (CI does a clean `npm ci`). Reports go to `test-results/`
and `ClientApp/test-results/` (git-ignored).

**Live tests:** tests that call real websites are marked `[Explicit]` + `[Category("Live")]`. They are skipped by
`dotnet test` and in CI. Run them on purpose with `dotnet test --filter "TestCategory=Live"`.

**GitLab runner:** jobs run on the instance runner tagged `not-ao-code-runner0` (`default: tags` in `.gitlab-ci.yml`).
If jobs stay "pending", change that tag. Runners are listed under *Settings → CI/CD → Runners*.

## Troubleshooting

### Angular build fails
```bash
cd ClientApp
npm ci
npm run build
```

### Database issues
The SQLite database is created automatically on first run. To reset:
```bash
rm FlatMonitor/data/listings.db
dotnet run
```

### More detailed scrape logs
All output goes through `ILogger`. At the default `Information` level you see pages, saved batches (every 20 flats)
and per-source summaries; failed detail pages and skipped items are `Warning`, failed sources `Error` (with the stack trace).
Per-flat and per-card output (and Playwright steps) is `Debug`; the first 200 characters of each IS24 card are `Trace`.
Raise the level for the scrapers only, for example:
```bash
Logging__LogLevel__FlatMonitor.Scraping=Debug dotnet run -- scrape
```

### Immowelt fails or has no details
The Immowelt error is in the scrape summary; the other sources are not affected and Immowelt keeps its previous flats.

| Message | Cause and fix |
|---|---|
| `No Chrome to attach to at http://127.0.0.1:9222` | `LaunchChrome` is off and no debug Chrome runs: start it by hand (above) |
| `Chrome not found (...)` | Chrome is installed elsewhere: set `Scrapers:Immowelt:ChromePath` |
| `Chrome started but does not answer ...` | A window of the `FlatMonitorChrome` profile is open without the debugging port: close it, scrape again |
| Warning `DataDome shows its robot check ...: pass it in the Chrome window` | Solve the check in the Chrome tab that came to the front; the scrape goes on by itself (CLI and UI alike) |
| `... blocked by DataDome, the robot check was not passed ...` | Nobody passed it within `RobotCheckWaitSeconds`: scrape again and solve it while it waits |
| `... no search data, has the page changed?` | immowelt changed its pages: the parsers need work (`explore-immowelt --cdp`, `SaveDebugPages`) |
| Warning `the connection to Chrome is lost` | Windows slept or Chrome was closed mid-run: the remaining flats keep their list data. Scrape plugged in |

### Port already in use
- Backend (5000): `netstat -ano | findstr :5000` (Windows) or `lsof -i :5000` (Linux/Mac)
- Frontend (4200): Modify `proxy.conf.json` if needed

## Environment Variables

See `.env.example` or create `.env` with:
```
OHNE_MAKLER_URL=https://www.ohne-makler.net/...
IMMO_SCOUT_URL=https://www.immobilienscout24.de/...
```
