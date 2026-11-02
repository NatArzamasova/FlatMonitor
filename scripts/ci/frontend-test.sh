#!/bin/sh
# CI job "frontend-test": Karma/Jasmine specs in headless Chrome (needs CHROME_BIN or a Chrome on PATH), collecting
# code coverage. Writes ClientApp/test-results/ (see karma.conf.js):
#   junit.xml                    test report (GitLab "Tests" tab, GitHub check run)
#   coverage-raw/cobertura.xml   raw coverage, for the job "frontend-coverage"
set -eu

cd ClientApp
# Clean install in CI (GitLab and GitHub set CI=true). Locally keep node_modules: npm ci deletes it first,
# which breaks a running `ng serve` and fails on Windows while files are in use.
if [ "${CI:-}" = "true" ] || [ ! -d node_modules ]; then
  npm ci
fi
rm -rf test-results/coverage-raw
npx ng test --watch=false --browsers=ChromeHeadlessCI --code-coverage
