#!/bin/sh
# CI job "frontend-coverage": reports from the raw coverage of "frontend-test" (its artifact ClientApp/test-results/).
# Report: test-results/coverage/frontend/ (see coverage-report.sh).
set -eu

sh scripts/ci/coverage-report.sh "FlatMonitor frontend" \
  "ClientApp/test-results/coverage-raw/cobertura.xml" \
  test-results/coverage/frontend
