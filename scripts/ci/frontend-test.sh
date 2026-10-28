#!/bin/sh
# CI job "frontend-test": Karma/Jasmine specs in headless Chrome (needs CHROME_BIN or a Chrome on PATH).
# Writes ClientApp/test-results/junit.xml (see karma.conf.js).
set -eu

cd ClientApp
# Clean install in CI (GitLab and GitHub set CI=true). Locally keep node_modules: npm ci deletes it first,
# which breaks a running `ng serve` and fails on Windows while files are in use.
if [ "${CI:-}" = "true" ] || [ ! -d node_modules ]; then
  npm ci
fi
npx ng test --watch=false --browsers=ChromeHeadlessCI
