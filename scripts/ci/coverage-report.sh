#!/bin/sh
# CI jobs "backend-coverage" and "frontend-coverage": turn the raw coverage of a test job into reports.
#
#   sh scripts/ci/coverage-report.sh <title> <raw cobertura file pattern> <report directory>
#
# Writes to the report directory:
#   Cobertura.xml        merged coverage (GitLab MR coverage view)
#   index.html           report to browse (artifact)
#   SummaryGithub.md     summary for the GitHub job summary
#   Summary.txt          printed to the log ("Line coverage: N%", read by GitLab's `coverage:` regex)
# Needs the sources checked out at the path the tests ran at (same in every job of a pipeline).
set -eu

title=$1
reports=$2
target=$3

rm -rf "$target"
dotnet tool restore
dotnet tool run reportgenerator \
  "-reports:$reports" \
  "-targetdir:$target" \
  "-reporttypes:Cobertura;Html;TextSummary;MarkdownSummaryGithub" \
  "-title:$title"

cat "$target/Summary.txt"
