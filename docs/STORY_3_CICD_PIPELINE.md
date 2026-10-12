# Story 3: CI/CD Pipeline — Scheduled Scrape & Artifact Commit

## Goal
Implement a GitLab CI/CD scheduled pipeline that runs the scraper daily, generates snapshot JSONL files, and commits them back to the repository.

## Acceptance Criteria
- [ ] `.gitlab-ci.yml` exists with a scheduled pipeline job
- [ ] Pipeline is configured to run on a schedule (e.g., daily at a specific time)
- [ ] Pipeline job executes `dotnet run -- scrape` in the repo
- [ ] New snapshot JSONL file is generated and placed in `data/snapshots/`
- [ ] Pipeline commits new snapshot file back to main branch with appropriate commit message
- [ ] Pipeline pushes committed changes back to origin/main
- [ ] Pipeline has a deploy token or push-capable credentials configured
- [ ] Snapshot file diffs are clean (append-only, no conflicts) for repeated runs
- [ ] Pipeline handles failure gracefully (logs output, fails loudly if scrape fails)

## Implementation Tasks

### Phase 1: GitLab Deploy Token Setup
- [ ] Create a GitLab project deploy token (in GitLab project settings → Access Tokens)
  - Name: "snapshot-updater" (or similar)
  - Scopes: `api`, `write_repository`
  - Expiry: as needed (annual recommended)
- [ ] Store token in GitLab project CI/CD variables:
  - Variable name: `DEPLOY_TOKEN`
  - Value: the token (masked, protected)
  - Variable name: `DEPLOY_USER` (default: `oauth2`)

### Phase 2: GitLab CI/CD Pipeline Configuration
- [ ] Create `.gitlab-ci.yml` with:
  - Image: `mcr.microsoft.com/dotnet/sdk:10.0` (or appropriate .NET 10 image)
  - Job: `scrape-and-commit`
    - Trigger: scheduled pipeline
    - Script:
      - Restore dependencies: `dotnet restore`
      - Build: `dotnet build --configuration Release`
      - Run scraper: `dotnet run --configuration Release -- scrape`
      - Configure git user (for commit): email, name
      - Check git status and stage snapshot file: `git add data/snapshots/*.jsonl`
      - Commit if there are changes: `git commit -m "chore: scrape snapshot at {timestamp}"`
      - Push back to origin/main: `git push https://${DEPLOY_USER}:${DEPLOY_TOKEN}@${CI_SERVER_HOST}/${CI_PROJECT_PATH}.git HEAD:main`
    - Artifacts: snapshot JSONL file (optional, for inspection in GitLab UI)
    - Only: `schedules` (run only on scheduled pipelines)

### Phase 3: Error Handling & Logging
- [ ] Pipeline should fail loudly if:
  - dotnet restore fails
  - dotnet build fails
  - dotnet run -- scrape fails (exit code != 0)
  - git commit fails (unexpected state)
  - git push fails (auth, permissions, conflicts)
- [ ] Capture scraper output and store as artifact or log for debugging
- [ ] Add timeout to prevent hanging jobs (e.g., 10 minutes)

### Phase 4: Schedule Configuration
- [ ] In GitLab project settings → Pipelines → Schedules:
  - Create scheduled pipeline trigger
  - Cron expression (e.g., `0 6 * * *` for daily at 6 AM UTC)
  - Target: `main` branch
  - Variables: none needed (deploy token is in project settings)

### Phase 5: Verification & Testing
- [ ] Manually trigger the pipeline once in GitLab UI (without waiting for schedule)
- [ ] Verify:
  - Build completes successfully
  - Scraper runs and outputs new snapshot file
  - Commit is created with correct message
  - Push to main succeeds
  - New snapshot file appears in repo on GitLab
  - No merge conflicts (append-only JSONL ensures this)

## Open Questions (Refine During Implementation)
- [ ] What should the commit message format be? (`chore: scrape snapshot at 2026-10-12T06:00:00Z`, or simpler?)
- [ ] Should we tag each scrape run (e.g., `snapshot-2026-10-12`)? Useful for releases/rollback?
- [ ] Should the pipeline also run the `report` command and commit a report file? Or is report manual-only?
- [ ] What's the desired scrape frequency? Daily, weekly, on-demand?
- [ ] Should failed scrapes be retried, or fail immediately?
- [ ] Do we need notifications (Slack, email) on pipeline success/failure?

## Notes
- **Deploy token scope**: must include `write_repository` to push changes back
- **Append-only JSONL format** ensures no merge conflicts even if multiple runs occur
- **No human merge conflicts** — snapshots are timestamped and never rewritten
- **Scheduled pipelines** in GitLab require at least one commit on the branch to create a schedule
- Alternative: GitHub Actions (if repo moves to GitHub later) — same principles, different syntax

## Post-Implementation
- Document the token setup and schedule details in the project README or ops runbook
- Consider adding a "dry-run" scrape option for testing pipeline without committing
