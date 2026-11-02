#!/bin/sh
# CI job "backend-coverage": reports from the raw coverage of "backend-test" (its artifact test-results/).
# Report: test-results/coverage/backend/ (see coverage-report.sh).
set -eu

sh scripts/ci/coverage-report.sh "FlatMonitor backend" \
  "test-results/coverage-raw/**/coverage.cobertura.xml" \
  test-results/coverage/backend
