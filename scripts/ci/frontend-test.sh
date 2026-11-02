#!/bin/sh
# CI job "frontend-test": Karma/Jasmine specs in headless Chrome (needs CHROME_BIN or a Chrome on PATH), with code
# coverage. Writes ClientApp/test-results/ (see karma.conf.js):
#   junit.xml                         test report (GitLab "Tests" tab, GitHub check run)
#   coverage/cobertura.xml            coverage (GitLab MR coverage view)
#   coverage/index.html               coverage report to browse (artifact)
#   coverage/SummaryGithub.md         coverage summary for the GitHub job summary (from coverage-summary.json)
# and prints the coverage summary to the log ("Lines : N%", read by GitLab's `coverage:` regex).
set -eu

cd ClientApp
# Clean install in CI (GitLab and GitHub set CI=true). Locally keep node_modules: npm ci deletes it first,
# which breaks a running `ng serve` and fails on Windows while files are in use.
if [ "${CI:-}" = "true" ] || [ ! -d node_modules ]; then
  npm ci
fi
rm -rf test-results/coverage
npx ng test --watch=false --browsers=ChromeHeadlessCI --code-coverage

node -e '
  const total = require("./test-results/coverage/coverage-summary.json").total;
  const row = (name, m) => `| ${name} | ${m.pct}% | ${m.covered} of ${m.total} |`;
  const lines = [
    "# Summary - FlatMonitor frontend",
    "",
    "| | Coverage | Covered |",
    "|:---|---:|---:|",
    row("**Lines**", total.lines),
    row("Statements", total.statements),
    row("Branches", total.branches),
    row("Functions", total.functions),
    "",
    "Full report: artifact `frontend-test-results`, `coverage/index.html`.",
  ];
  require("fs").writeFileSync("test-results/coverage/SummaryGithub.md", lines.join("\n") + "\n");
'
