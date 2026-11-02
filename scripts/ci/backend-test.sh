#!/bin/sh
# CI job "backend-test": run the compiled tests from backend-build, without rebuilding, collecting code coverage.
# [Explicit] tests (category "Live", they call real websites) are skipped.
#
# Writes test-results/:
#   backend-junit.xml                         test report (GitLab "Tests" tab, GitHub check run)
#   coverage-raw/<guid>/coverage.cobertura.xml   raw coverage of FlatMonitor, for the job "backend-coverage"
set -eu

rm -rf test-results/coverage-raw

dotnet test FlatMonitor.Tests/bin/Release/net10.0/FlatMonitor.Tests.dll \
  --logger "junit;LogFilePath=$PWD/test-results/backend-junit.xml" \
  --settings FlatMonitor.Tests/coverage.runsettings \
  --collect "XPlat Code Coverage" \
  --results-directory test-results/coverage-raw
