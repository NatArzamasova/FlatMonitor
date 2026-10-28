#!/bin/sh
# CI job "frontend-build": production build of the Angular app into ClientApp/dist.
set -eu

cd ClientApp
# Clean install in CI (GitLab and GitHub set CI=true). Locally keep node_modules: npm ci deletes it first,
# which breaks a running `ng serve` and fails on Windows while files are in use.
if [ "${CI:-}" = "true" ] || [ ! -d node_modules ]; then
  npm ci
fi
npx ng build
