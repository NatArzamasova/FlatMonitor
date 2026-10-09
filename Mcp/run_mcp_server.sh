#!/bin/bash
# Script to run the weather MCP server

cd "$(dirname "$0")"
python mcp_weather_server.py
