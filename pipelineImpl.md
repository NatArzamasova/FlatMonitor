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
commit-messages
backend-build  ──► backend-test        (scripts/ci/backend-build.sh  → scripts/ci/backend-test.sh)
frontend-build ──► frontend-test       (scripts/ci/frontend-build.sh → scripts/ci/frontend-test.sh)
```
Five jobs (decided during implementation: build and test are separate jobs, so a red job shows at a glance whether it's
a compile or a test failure). The three left-hand jobs start in parallel; each test job starts when its build job is green.
- `backend-test` runs the **compiled** `FlatMonitor.Tests.dll` from `backend-build`'s artifact (`dotnet test <dll>`),
  no second compile. The artifact is `FlatMonitor.Tests/bin/Release/net10.0/` **without** `.playwright/` (Playwright's
  browser driver, ~80 MB, only used by the scraper): about 81 MB instead of 158 MB.
- `frontend-test` only waits for `frontend-build`; `ng test` compiles on its own, so it doesn't download `dist/`.

- **Trigger:** every push to any branch. GitLab additionally runs merge request pipelines; a `workflow:` rule prevents
  a duplicate branch + MR pipeline for the same commit. GitHub: `push` only (the push run's result is shown on a PR too),
  plus a manual "Run workflow" button (`workflow_dispatch`).
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

### 1. Live scraper test: only run when asked ✅ DONE
`FlatMonitor.Tests/OhneMaklerScraperTests.cs`, test `OhneMaklerScraper_ScrapeAllListingsAsync_ReturnsListOfListings`:
- add `[Explicit("Calls the live ohne-makler.net site")]` and `[Category("Live")]`.
- `[Explicit]`: NUnit skips it in every normal run (`dotnet test` locally and in CI).
- To run it on purpose: `dotnet test --filter "TestCategory=Live"` (or select it in the IDE's test explorer).
- CI needs no special filter.

**Status:** done. `dotnet test` → 278 passed (5 s instead of ~20 s); `--filter "TestCategory=Live"` → runs the live test.

### 2. Commit-message check shared by hook and CI ✅ DONE
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

**Status:** done. Also added **`.gitattributes`** (`*.sh` and `scripts/hooks/*` → `eol=lf`): with `core.autocrlf=true`
Windows checkouts would otherwise get CRLF, and `sh` fails on CRLF. Checked: hook mode (good/bad message), range over the
last 10 commits, unknown `from` (falls back to the tip), a range containing a `wip` commit (in a throwaway clone) → listed, exit 1.
**After pulling this change, run `bash scripts/setup-hooks.sh` once**: the installed hook is a copy of the old one.

### 3. Frontend: Karma CI launcher + JUnit output ✅ DONE
- `ng generate config karma` → `ClientApp/karma.conf.js`, then:
  - custom launcher `ChromeHeadlessCI`: `base: 'ChromeHeadless'`, `flags: ['--no-sandbox', '--disable-dev-shm-usage']`;
  - `karma-junit-reporter` (devDependency) → `ClientApp/test-results/junit.xml`.
- `angular.json` → `test.options.karmaConfig: "karma.conf.js"`.
- Locally `npm test` keeps working as before (normal Chrome, watch mode).
- `.gitignore`: add `ClientApp/test-results/`.

**Status:** done (`karma-junit-reporter` 2.0.1). 343 specs pass with `ChromeHeadlessCI`; `junit.xml` has 343 test cases.

### 4. Backend: JUnit output ✅ DONE
- Add `JunitXml.TestLogger` to `Directory.Packages.props` and `FlatMonitor.Tests.csproj`.
- `.gitignore`: add `test-results/` (also `.nuget/`, `.npm/`: GitLab's cache folders inside the project directory).

**Status:** done (`JunitXml.TestLogger` 8.1.0, listed under "Test packages"). Report has 279 cases: 278 passed, 1 skipped (live).

### 5. Shared CI scripts (`scripts/ci/`) ✅ DONE
One script per job; both pipelines call them, and they run locally in Git Bash to reproduce a CI failure:

| Script | Does |
|---|---|
| `backend-build.sh` | `dotnet restore` + `dotnet build -c Release` |
| `backend-test.sh` | `dotnet test FlatMonitor.Tests/bin/Release/net10.0/FlatMonitor.Tests.dll` + JUnit logger |
| `frontend-build.sh` | `npm ci` (see below) + `ng build` |
| `frontend-test.sh` | `npm ci` (see below) + `ng test --watch=false --browsers=ChromeHeadlessCI` |

All use `set -eu`. The frontend scripts run `npm ci` only when `CI=true` (set by GitLab and GitHub) or `node_modules` is
missing. Locally they keep `node_modules`: `npm ci` deletes it first, which broke a running `ng serve` during testing and
fails on Windows while files are in use.

**Status:** all four run green locally (build → test order). Also checked: tests run from a copy of the output without `.playwright`.

### 6. `.gitlab-ci.yml` ✅ DONE
See the file. Notes:
- `default: tags: [not-ao-code-runner0]`: pins all jobs to that instance runner (the first in the list). If jobs stay
  "pending", change or remove this line; ask the IT team (`it-kuemmerlinge`) which tag fits jobs that need Docker + internet.
- `commit-messages` uses `alpine:3.20` + `apk add git` (no guessing of an `alpine/git` tag) and sets `safe.directory`
  (git refuses repos owned by another user in containers).
- Artifacts: backend test binaries 1 day (excluding `.playwright/`), test results and `ClientApp/dist/` 1 week.
- NuGet cache in `backend-build`; npm cache filled by `frontend-build`, only pulled by `frontend-test`.
- Chromium is installed in `frontend-test` (`apt-get`, about 30–60 s). If that gets annoying, switch to an image with Chrome.
- Not run yet: needs a push. Validate first in *Build → Pipeline editor → Validate*.

### 7. `.github/workflows/ci.yml` ✅ DONE
See the file. Same five jobs on `ubuntu-latest`; `actions/setup-dotnet` 10.0.x, `actions/setup-node` 20 with npm cache,
`actions/cache` for NuGet. Test binaries are passed via `upload-artifact`/`download-artifact` (hidden folders such as
`.playwright` are skipped by `upload-artifact` by default). Test reports via `dorny/test-reporter` (`checks: write`).
Triggers: every branch push + manual `workflow_dispatch`; `concurrency` cancels outdated runs. Not run yet: needs a push.

Both YAML files parse, and their job graphs were checked (test jobs need their build job; the others are independent).

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

### 9. Docs ✅ DONE
- `BUILD_AND_RUN.md`: section "CI": what runs where, how to reproduce a job locally (`sh scripts/ci/backend.sh`,
  `sh scripts/ci/frontend.sh`), how to run the live test (`dotnet test --filter "TestCategory=Live"`).
- `CLAUDE.md` / `AGENTS.md`: one line under "Testing" about the `[Explicit]` live test and the pipelines.

**Status:** done. `BUILD_AND_RUN.md` has a "CI Pipelines" section (jobs table, local reproduction, live tests, runner tag).

## Verification
1. GitLab: validate `.gitlab-ci.yml` in the pipeline editor.
2. Push to **GitLab** → pipeline appears under Build → Pipelines; all five jobs green; "Tests" tab shows
   279 backend tests (278 passed + 1 skipped live test) and 343 frontend tests.
   ✅ **All five jobs green** on runner `not-ao-code-runner0` (first push of `chore(ci): add GitLab and GitHub pipelines`).
   ⏳ The Tests tab could not be checked: right after the run, the whole GitLab instance returned 500 errors
   (a general outage, unrelated to this project). Check the test counts once GitLab is back.
3. Push to **GitHub** → run appears in the Actions tab; all five jobs green; "Backend tests" /
   "Frontend tests" check runs list the tests.
   ✅ **Works.**
4. Negative checks on a throwaway branch (on both platforms):
   - a commit with message `wip` → `commit-messages` fails and lists it;
   - a deliberately failing backend test → `backend-test` fails, the test is named in the report;
   - a deliberately failing frontend spec → `frontend-test` fails, the spec is named in the report.
   (Optional; not done yet.)
5. Push twice quickly → the older run is cancelled.
6. The second run is faster (NuGet/npm cache hit).
7. Locally: `dotnet test` no longer calls ohne-makler.net; `dotnet test --filter "TestCategory=Live"` does. ✅

## Suggested commit sequence
1. `test(tests): run the live ohne-makler scraper test only when asked`
2. `chore(ci): share commit message check between hook and CI` (incl. `.gitattributes`)
3. `chore(ci): add karma CI launcher and junit test reports`
4. `chore(ci): add GitLab and GitHub pipelines with shared CI scripts`
5. `docs(ci): document the pipelines`

## Open questions
1. ~~**GitLab runner**~~ **Resolved:** the instance runner `not-ao-code-runner0` runs the Docker images and has internet
   access; all jobs passed on it.
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
