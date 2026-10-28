#!/bin/sh
# CI job "backend-test": run the compiled tests from backend-build, without rebuilding.
# [Explicit] tests (category "Live", they call real websites) are skipped.
set -eu

dotnet test FlatMonitor.Tests/bin/Release/net10.0/FlatMonitor.Tests.dll \
  --logger "junit;LogFilePath=$PWD/test-results/backend-junit.xml"
