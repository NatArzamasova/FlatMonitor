# Building and Running FlatMonitor

## Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm
- Environment variables in `.env` (for scraping credentials)

## Development Setup

### Option 1: Run Web Server (with Angular SPA)

1. **Build Angular frontend**
   ```bash
   cd ClientApp
   npm install
   npm run build
   cd ..
   ```

2. **Run ASP.NET Core web server**
   ```bash
   dotnet run
   ```
   
   The application will start on `http://localhost:5000`

### Option 2: Parallel Development (Recommended for Frontend Changes)

Terminal 1 - Backend:
```bash
dotnet run
```

Terminal 2 - Frontend (with live reload):
```bash
cd ClientApp
npm install (if first time)
npm start
```

The Angular dev server runs on `http://localhost:4200` and proxies API calls to `http://localhost:5000`.

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
├── Api/Models/           # API response models
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
