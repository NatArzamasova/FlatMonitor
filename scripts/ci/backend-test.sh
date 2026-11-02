#!/bin/sh
# CI job "backend-test": run the compiled tests from backend-build, without rebuilding, with code coverage.
# [Explicit] tests (category "Live", they call real websites) are skipped.
#
# Writes test-results/:
#   backend-junit.xml                 test report (GitLab "Tests" tab, GitHub check run)
#   coverage/Cobertura.xml            coverage of FlatMonitor (GitLab MR coverage view)
#   coverage/index.html               coverage report to browse (artifact)
#   coverage/SummaryGithub.md         coverage summary for the GitHub job summary
# and prints the line coverage to the log ("Line coverage: N%", read by GitLab's `coverage:` regex).
set -eu

rm -rf test-results/coverage-raw test-results/coverage

dotnet test FlatMonitor.Tests/bin/Release/net10.0/FlatMonitor.Tests.dll \
  --logger "junit;LogFilePath=$PWD/test-results/backend-junit.xml" \
  --settings FlatMonitor.Tests/coverage.runsettings \
  --collect "XPlat Code Coverage" \
  --results-directory test-results/coverage-raw

# One report from the collector's file (it lands in a folder named by a GUID)
dotnet tool restore
dotnet tool run reportgenerator \
  "-reports:test-results/coverage-raw/**/coverage.cobertura.xml" \
  "-targetdir:test-results/coverage" \
  "-reporttypes:Cobertura;Html;TextSummary;MarkdownSummaryGithub" \
  "-title:FlatMonitor backend"

cat test-results/coverage/Summary.txt
