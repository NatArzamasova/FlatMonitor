#!/usr/bin/env python3
"""
MCP Server for Weather Tools
Implements the Model Context Protocol with a get_weather tool
"""

import os
from pathlib import Path
import json
import httpx

from mcp.server import Server
from mcp.types import Tool, TextContent, ToolResult

# Load API key from .env file
env_file = Path(__file__).parent / ".env"
if env_file.exists():
    with open(env_file, 'r') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#') and '=' in line:
                key, value = line.split('=', 1)
                os.environ[key.strip()] = value.strip()

NEXOS_API_KEY = os.getenv("ANTHROPIC_API_KEY")
NEXOS_BASE_URL = "https://api.nexos.ai/v1"

# Create MCP server
server = Server("weather-mcp-server")


def get_weather(location: str) -> dict:
    """Get weather for a location using a real weather API or mock data."""
    # Mock implementation - in production, call a real weather API
    weather_data = {
        "San Francisco": {"temperature": 72, "condition": "Sunny", "humidity": 65},
        "New York": {"temperature": 65, "condition": "Cloudy", "humidity": 70},
        "London": {"temperature": 55, "condition": "Rainy", "humidity": 85},
        "Tokyo": {"temperature": 68, "condition": "Partly Cloudy", "humidity": 60},
    }

    if location.title() in weather_data:
        return weather_data[location.title()]
    else:
        return {"temperature": "Unknown", "condition": "Data not available", "humidity": "N/A"}


@server.call_tool()
async def handle_call_tool(name: str, arguments: dict):
    """Handle tool calls."""
    if name == "get_weather":
        location = arguments.get("location", "Unknown")
        result = get_weather(location)
        return [ToolResult(
            content=[TextContent(
                type="text",
                text=json.dumps(result)
            )]
        )]
    else:
        return [ToolResult(
            content=[TextContent(
                type="text",
                text=f"Unknown tool: {name}"
            )],
            isError=True
        )]


@server.list_tools()
async def handle_list_tools():
    """List available tools."""
    return [
        Tool(
            name="get_weather",
            description="Get current weather for a location",
            inputSchema={
                "type": "object",
                "properties": {
                    "location": {
                        "type": "string",
                        "description": "City name or location (e.g., 'San Francisco', 'New York', 'London')"
                    }
                },
                "required": ["location"]
            }
        )
    ]


if __name__ == "__main__":
    import asyncio

    # Run the server
    async def main():
        async with server:
            print("Weather MCP Server is running...")
            print("Tools available:")
            tools = await handle_list_tools()
            for tool in tools:
                print(f"  - {tool.name}: {tool.description}")

            # Keep the server running
            await asyncio.sleep(float('inf'))

    asyncio.run(main())
