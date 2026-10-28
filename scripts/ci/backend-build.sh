#!/bin/sh
# CI job "backend-build": restore and compile the solution (Release).
# The test job runs FlatMonitor.Tests/bin/Release/net10.0 from this job's artifact.
set -eu

dotnet restore FlatMonitor.sln
dotnet build FlatMonitor.sln -c Release --no-restore
