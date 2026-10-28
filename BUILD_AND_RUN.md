# Building and Running FlatMonitor

## Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm
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

## Project Structure

```
FlatMonitor/               # .NET Backend
├── Api/Controllers/       # REST API endpoints
├── Api/Contracts/        # API request and response contracts
├── Models/               # Domain models (Flat, FlatSnapshot)
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

- `GET /api/flats/aggregated` — Returns aggregated flat statistics
- `POST /api/scrape` — Triggers manual scrape
- `GET /swagger` — Swagger UI (development only)

## CI Pipelines

Every push runs a pipeline on the platform it was pushed to: `.gitlab-ci.yml` on GitLab (`origin`),
`.github/workflows/ci.yml` on GitHub (`GitHubFlatMonitor`). Both run the same five jobs:

| Job | Script | Checks |
|---|---|---|
| commit-messages | `scripts/check-commit-msg.sh --range <from>..<to>` | every pushed commit matches `type(scope): message` |
| backend-build | `scripts/ci/backend-build.sh` | `dotnet restore` + `dotnet build -c Release` |
| backend-test | `scripts/ci/backend-test.sh` | NUnit tests from the build's output, JUnit report |
| frontend-build | `scripts/ci/frontend-build.sh` | `ng build` (production) |
| frontend-test | `scripts/ci/frontend-test.sh` | Karma/Jasmine specs in headless Chrome, JUnit report |

The test jobs start once their build job is green. Failed tests are listed by name in GitLab's pipeline "Tests" tab /
the "Backend tests" and "Frontend tests" check runs on GitHub.

**Reproduce a job locally** (Git Bash, from the repository root):
```bash
sh scripts/ci/backend-build.sh && sh scripts/ci/backend-test.sh
sh scripts/ci/frontend-build.sh && sh scripts/ci/frontend-test.sh
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

### Port already in use
- Backend (5000): `netstat -ano | findstr :5000` (Windows) or `lsof -i :5000` (Linux/Mac)
- Frontend (4200): Modify `proxy.conf.json` if needed

## Environment Variables

See `.env.example` or create `.env` with:
```
OHNE_MAKLER_URL=https://www.ohne-makler.net/...
IMMO_SCOUT_URL=https://www.immobilienscout24.de/...
```
