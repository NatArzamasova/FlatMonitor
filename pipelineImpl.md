# CI Pipeline: Implementation Plan

## Goal

Every push runs an automatic pipeline on the platform it was pushed to:

| Pushed to | Remote | Runs |
|---|---|---|
| GitLab (`code.andrena.de/csharp/workinprogress`) | `origin` | `.gitlab-ci.yml` (GitLab CI) |
| GitHub (`NatArzamasova/FlatMonitor`) | `GitHubFlatMonitor` | `.github/workflows/ci.yml` (GitHub Actions) |

Both files live in the repository. GitLab ignores `.github/` and GitHub ignores `.gitlab-ci.yml`, so a push to one
remote only triggers that remote's pipeline. A push to both remotes runs both.

Each pipeline tells within a few minutes whether the commit is OK:
- the commit messages follow `type(scope): message`,
- the .NET backend builds and all its tests pass (live-site tests excluded),
- the Angular frontend builds (production build) and all its tests pass.

A red pipeline shows which part failed, and failed tests are listed by name.

**To keep both pipelines identical**, the actual work is done by shell scripts in `scripts/ci/`. The two YAML files only
prepare the environment (images, SDKs, caches) and call the same scripts. A change to "what CI checks" is made once.

**Out of scope:** deploying and scheduled scraping (see "Later" at the end).

## Findings from the repository

| Topic | Finding | Consequence for CI |
|---|---|---|
| Existing CI | No `.gitlab-ci.yml`, no `.github/` | Start from scratch |
| Backend | .NET 10 (`net10.0`, local SDK 10.0.401), central package versions, NUnit + Moq + FluentAssertions | GitLab image `mcr.microsoft.com/dotnet/sdk:10.0`; GitHub `actions/setup-dotnet` 10.0.x |
| Backend tests | 279 tests, about 20 s; fixtures are committed (`FlatMonitor.Tests/Fixtures/`) | Run as-is |
| **Live test** | `OhneMaklerScraperTests.OhneMaklerScraper_ScrapeAllListingsAsync_ReturnsListOfListings` calls the **real** ohne-makler.net | Mark it `[Explicit]`, so it only runs when asked for (step 1) |
| Playwright | Only used in production code (IS24 browser scraper), not in tests | No browsers needed for the backend |
| Secrets | Tests don't read `.env` or credentials | No CI variables / secrets needed |
| Frontend | Angular 18, Karma + Jasmine, `package-lock.json` committed, local Node 20.11 | Node 20 + Chrome; `npm ci` |
| Frontend tests | 343 specs in Chrome; no `karma.conf.js` (Angular default) | Chrome in a container as root needs `--no-sandbox` → CI launcher (step 3) |
| Frontend build | `ng build` passes; prints a pre-existing CSS warning ("1 rules skipped due to selector errors") | Doesn't fail the build; fix separately if wanted |
| Commit format | Enforced only locally by `scripts/hooks/commit-msg` (bash, only if `setup-hooks.sh` was run) | Re-check in CI with one script shared by hook and CI (step 2) |

## Pipeline design (same on both platforms)

```
commit-messages          backend                        frontend
(check the pushed   ┐    restore → build → test   ┐     npm ci → build → test
 commits)           │    (scripts/ci/backend.sh)  │     (scripts/ci/frontend.sh)
                    └── all three run in parallel ┘
```

- **Trigger:** every push to any branch. GitLab additionally runs merge request pipelines; a `workflow:` rule prevents
  a duplicate branch + MR pipeline for the same commit. GitHub: `push` only (the push run's result is shown on a PR too),
  plus a manual "Run workflow" button (`workflow_dispatch`).
- **Parallel:** the three jobs don't depend on each other, so you get all three results in one run.
- **Cancel outdated runs:** pushing again cancels the still-running pipeline of the same branch
  (GitLab `interruptible: true`; GitHub `concurrency` with `cancel-in-progress`).
- **Caches:** NuGet packages keyed on `Directory.Packages.props`, npm cache keyed on `ClientApp/package-lock.json`.
- **Timeouts:** 15 min per job. A hanging job fails loudly instead of blocking a runner.
- **Fail loudly:** every script runs with `set -eu`. Any failing command (restore, build, test) fails the job with a non-zero exit code.
- **Test reports:** both test runs write JUnit XML. GitLab shows it in the pipeline's "Tests" tab and in MRs. GitHub shows
  it as a check run ("Backend tests" / "Frontend tests") with failed tests listed.
- **Artifacts:** the test result files (always, also on failure) and `ClientApp/dist/` (1 week), so a build of any commit
  can be downloaded.

## Implementation steps

### 1. Live scraper test: only run when asked
`FlatMonitor.Tests/OhneMaklerScraperTests.cs`, test `OhneMaklerScraper_ScrapeAllListingsAsync_ReturnsListOfListings`:
- add `[Explicit("Calls the live ohne-makler.net site")]` and `[Category("Live")]`.
- `[Explicit]`: NUnit skips it in every normal run (`dotnet test` locally and in CI).
- To run it on purpose: `dotnet test --filter "TestCategory=Live"` (or select it in the IDE's test explorer).
- CI needs no special filter.

### 2. Commit-message check shared by hook and CI
- New `scripts/check-commit-msg.sh` (POSIX `sh`, no bash, so it also runs in a minimal Alpine image):
  - `check-commit-msg.sh <message-file>`: checks the first line (what the hook does today).
  - `check-commit-msg.sh --range <from>..<to>`: checks every non-merge commit in the range
    (`git log --no-merges --format='%h %s'`), prints every bad subject, exits 1 if any is bad.
  - Same regex as today: `^(feat|fix|refactor|docs|test|chore)\([a-z0-9_-]+\): .+`.
- `scripts/hooks/commit-msg` becomes a one-liner calling `scripts/check-commit-msg.sh "$1"`, so the rule lives in one place.
- Merge commits ("Merge branch …", created by GitLab/GitHub) are skipped.
- **Which commits are checked** (computed in each YAML, passed as `--range`):

  | Situation | GitLab | GitHub |
  |---|---|---|
  | Push to an existing branch | `$CI_COMMIT_BEFORE_SHA..$CI_COMMIT_SHA` | `${{ github.event.before }}..${{ github.sha }}` |
  | First push of a new branch (before = all zeros) | the pushed tip only: `$CI_COMMIT_SHA~1..$CI_COMMIT_SHA` | `${{ github.sha }}~1..${{ github.sha }}` |
  | Merge request | `$CI_MERGE_REQUEST_DIFF_BASE_SHA..$CI_COMMIT_SHA` | — (push only) |

  Both need the full history checked out (GitLab `GIT_DEPTH: 0`, GitHub `fetch-depth: 0`).
  Force-pushes whose old tip no longer exists: fall back to "tip only" (the script handles an unknown `from`).

### 3. Frontend: Karma CI launcher + JUnit output
- `ng generate config karma` → `ClientApp/karma.conf.js`, then:
  - custom launcher `ChromeHeadlessCI`: `base: 'ChromeHeadless'`, `flags: ['--no-sandbox', '--disable-dev-shm-usage']`;
  - `karma-junit-reporter` (devDependency) → `ClientApp/test-results/junit.xml`.
- `angular.json` → `test.options.karmaConfig: "karma.conf.js"`.
- Locally `npm test` keeps working as before (normal Chrome, watch mode).
- `.gitignore`: add `ClientApp/test-results/`.

### 4. Backend: JUnit output
- Add `JunitXml.TestLogger` to `Directory.Packages.props` and `FlatMonitor.Tests.csproj`.
- `.gitignore`: add `test-results/`.

### 5. Shared CI scripts (`scripts/ci/`)
Both pipelines call these; they can also be run locally (Git Bash) to reproduce a CI failure.

`scripts/ci/backend.sh`:
```sh
#!/bin/sh
set -eu
dotnet restore FlatMonitor.sln
dotnet build FlatMonitor.sln -c Release --no-restore
dotnet test FlatMonitor.sln -c Release --no-build \
  --logger "junit;LogFilePath=$PWD/test-results/backend-junit.xml"
```

`scripts/ci/frontend.sh`:
```sh
#!/bin/sh
set -eu
cd ClientApp
npm ci
npx ng build
npx ng test --watch=false --browsers=ChromeHeadlessCI
```

### 6. `.gitlab-ci.yml` (draft)
```yaml
workflow:
  rules:
    # Merge request open → MR pipeline only; otherwise a branch pipeline. Avoids duplicates.
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
    - if: $CI_COMMIT_BRANCH && $CI_OPEN_MERGE_REQUESTS
      when: never
    - if: $CI_COMMIT_BRANCH

default:
  interruptible: true
  timeout: 15 minutes

commit-messages:
  image:
    name: alpine/git:2.45.2
    entrypoint: [""]
  variables:
    GIT_DEPTH: 0
  script:
    - |
      if [ -n "${CI_MERGE_REQUEST_DIFF_BASE_SHA:-}" ]; then
        range="$CI_MERGE_REQUEST_DIFF_BASE_SHA..$CI_COMMIT_SHA"
      elif [ "$CI_COMMIT_BEFORE_SHA" = "0000000000000000000000000000000000000000" ]; then
        range="$CI_COMMIT_SHA~1..$CI_COMMIT_SHA"
      else
        range="$CI_COMMIT_BEFORE_SHA..$CI_COMMIT_SHA"
      fi
      sh scripts/check-commit-msg.sh --range "$range"

backend:
  image: mcr.microsoft.com/dotnet/sdk:10.0
  variables:
    NUGET_PACKAGES: $CI_PROJECT_DIR/.nuget/packages
    DOTNET_CLI_TELEMETRY_OPTOUT: "1"
    DOTNET_NOLOGO: "1"
  cache:
    key:
      files: [Directory.Packages.props]
    paths: [.nuget/packages]
  script:
    - sh scripts/ci/backend.sh
  artifacts:
    when: always
    reports:
      junit: test-results/backend-junit.xml

frontend:
  image: node:20-bookworm
  variables:
    CHROME_BIN: /usr/bin/chromium
    npm_config_cache: $CI_PROJECT_DIR/.npm
  cache:
    key:
      files: [ClientApp/package-lock.json]
    paths: [.npm]
  before_script:
    - apt-get update && apt-get install -y --no-install-recommends chromium
  script:
    - sh scripts/ci/frontend.sh
  artifacts:
    when: always
    expire_in: 1 week
    paths: [ClientApp/dist/]
    reports:
      junit: ClientApp/test-results/junit.xml
```
- No `stages:` needed: all jobs are in the default `test` stage and run in parallel.
- Installing Chromium costs about 30–60 s per run. If that gets annoying, switch to an image with Node 20 + Chrome
  preinstalled (e.g. a pinned `cypress/browsers` tag). Not needed for a first version.

### 7. `.github/workflows/ci.yml` (draft)
```yaml
name: CI

on:
  push:
    branches: ['**']
  workflow_dispatch:

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read
  checks: write          # test-reporter creates the "… tests" check runs

jobs:
  commit-messages:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - name: Check commit messages
        env:
          BEFORE: ${{ github.event.before }}
          AFTER: ${{ github.sha }}
        run: |
          if [ -z "$BEFORE" ] || [ "$BEFORE" = "0000000000000000000000000000000000000000" ]; then
            range="$AFTER~1..$AFTER"
          else
            range="$BEFORE..$AFTER"
          fi
          sh scripts/check-commit-msg.sh --range "$range"

  backend:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    env:
      DOTNET_CLI_TELEMETRY_OPTOUT: "1"
      DOTNET_NOLOGO: "1"
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ hashFiles('Directory.Packages.props') }}
      - run: sh scripts/ci/backend.sh
      - uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Backend tests
          path: test-results/backend-junit.xml
          reporter: java-junit
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: backend-test-results
          path: test-results/

  frontend:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm
          cache-dependency-path: ClientApp/package-lock.json
      - run: sh scripts/ci/frontend.sh      # Chrome is preinstalled on ubuntu-latest
      - uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Frontend tests
          path: ClientApp/test-results/junit.xml
          reporter: java-junit
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: frontend
          path: |
            ClientApp/test-results/
            ClientApp/dist/
          retention-days: 7
```

### 8. Platform settings (manual, in the web UI)

**GitLab** (project `csharp/workinprogress` on code.andrena.de; needs the *Maintainer* or *Owner* role):
- Open the project → left sidebar **Settings → CI/CD** (direct link:
  `https://code.andrena.de/csharp/workinprogress/-/settings/ci_cd`) → expand **Runners**.
  - There must be at least one available runner (project, group or instance runner) that uses the **Docker executor**.
    Without one, pipelines stay "pending". If none is listed, ask the GitLab admins at andrena which runners are available.
  - If the left sidebar has no "Settings" entry, your role is too low → ask the project owner.
- Network: the runner must reach `mcr.microsoft.com`, Docker Hub, `api.nuget.org`, `registry.npmjs.org` and Debian
  mirrors (for Chromium). With a company proxy/registry mirror, adjust image names and set `HTTP(S)_PROXY` as CI/CD variables
  (same settings page → **Variables**).
- Pipelines: left sidebar **Build → Pipelines**. Validate the YAML before the first push: **Build → Pipeline editor → Validate**.
- Optional: **Settings → Merge requests → "Pipelines must succeed"**.

**GitHub** (repo `NatArzamasova/FlatMonitor`):
- **Settings → Actions → General**: "Allow all actions and reusable workflows" (or at least GitHub's + `dorny/test-reporter`);
  under "Workflow permissions" the default is fine (the workflow asks for `checks: write` itself).
- Runs: the **Actions** tab. Hosted `ubuntu-latest` runners need no setup. Private repos use the free monthly
  Actions minutes; one run is about 3–5 min.
- Optional: **Settings → Branches → Branch protection rule** for `main` with "Require status checks to pass".

### 9. Docs
- `BUILD_AND_RUN.md`: section "CI": what runs where, how to reproduce a job locally (`sh scripts/ci/backend.sh`,
  `sh scripts/ci/frontend.sh`), how to run the live test (`dotnet test --filter "TestCategory=Live"`).
- `CLAUDE.md` / `AGENTS.md`: one line under "Testing" about the `[Explicit]` live test and the pipelines.

## Verification
1. GitLab: validate `.gitlab-ci.yml` in the pipeline editor.
2. Push a branch to **GitLab** → pipeline appears under Build → Pipelines; all three jobs green; "Tests" tab shows about
   278 backend tests (279 minus the explicit live test) and 343 frontend tests.
3. Push the same branch to **GitHub** → run appears in the Actions tab; all three jobs green; "Backend tests" /
   "Frontend tests" check runs list the tests.
4. Negative checks on a throwaway branch (on both platforms):
   - a commit with message `wip` → `commit-messages` fails and lists it;
   - a deliberately failing backend test → `backend` fails, the test is named in the report;
   - a deliberately failing frontend spec → `frontend` fails, the spec is named in the report.
5. Push twice quickly → the older run is cancelled.
6. The second run is faster (NuGet/npm cache hit).
7. Locally: `dotnet test` no longer calls ohne-makler.net; `dotnet test --filter "TestCategory=Live"` does.

## Suggested commit sequence
1. `test(tests): run the live ohne-makler scraper test only when asked`
2. `chore(ci): share commit message check between hook and CI`
3. `chore(ci): add karma CI launcher and junit test reports`
4. `chore(ci): add shared CI scripts`
5. `chore(ci): add GitLab pipeline`
6. `chore(ci): add GitHub Actions workflow`
7. `docs(ci): document the pipelines`

## Open questions
1. **GitLab runner:** is a runner with Docker executor available for `csharp/workinprogress`, and does it need a proxy or an
   internal registry mirror? (Check as described in step 8.)
2. **GitHub repo visibility:** public (unlimited Actions minutes) or private (monthly quota)? Only matters for cost.

## Later: scheduled scrape (recycled from the old Story 3)
The old Story 3 planned a daily GitLab pipeline that runs `dotnet run -- scrape` and commits snapshot JSONL files back
to `main`. That doesn't fit the current architecture: SQLite (`FlatMonitor/data/listings.db`) is the source of
truth and git-ignored, and snapshots are debugging backups that are never committed. A CI runner would scrape into a
throw-away database. If scheduled scraping is wanted later, it needs a place where the database lives (a server, a
persistent volume, or a hosted DB) first. Points from Story 3 that still apply then:
- **Schedule:** GitLab **Build → Pipeline schedules** (cron, e.g. `0 6 * * *`, target `main`); GitHub `on: schedule`.
  The job runs only for schedules (GitLab `rules: - if: $CI_PIPELINE_SOURCE == "schedule"`).
- **Credentials:** a project access token with only the scopes it needs, stored as a masked/protected CI variable,
  never in the repo.
- **Fail loudly:** `dotnet run -- scrape` already exits non-zero on failure; add a job timeout (scraping takes longer
  than tests, e.g. 30 min) and keep the scraper output as an artifact for debugging.
- **First run:** trigger the schedule manually once ("Run pipeline schedule") and check the result before relying on it.
- **Notifications:** GitLab **Settings → Integrations** / GitHub notification settings for failed scheduled runs.
