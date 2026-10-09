#!/usr/bin/env python3
"""
MCP Server implementation using Nexos Gateway.
Provides example tools that can be called by Claude.
"""

import json
import os
from pathlib import Path
from typing import Any

import httpx

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


def process_tool_call(tool_name: str, tool_input: dict[str, Any]) -> str:
    """Process tool calls and return results."""
    if tool_name == "get_weather":
        location = tool_input.get("location", "Unknown")
        return json.dumps({"location": location, "temperature": 72, "condition": "Sunny"})

    elif tool_name == "calculate":
        operation = tool_input.get("operation", "add")
        a = tool_input.get("a", 0)
        b = tool_input.get("b", 0)

        if operation == "add":
            result = a + b
        elif operation == "subtract":
            result = a - b
        elif operation == "multiply":
            result = a * b
        elif operation == "divide":
            result = a / b if b != 0 else "Error: Division by zero"
        else:
            result = "Unknown operation"

        return json.dumps({"result": result, "operation": operation})

    else:
        return json.dumps({"error": f"Unknown tool: {tool_name}"})


def run_mcp_server():
    """Run the MCP server with tool definitions."""
    if not NEXOS_API_KEY:
        print("ERROR: ANTHROPIC_API_KEY not set in .env file")
        return

    tools = [
        {
            "name": "get_weather",
            "description": "Get current weather for a location",
            "input_schema": {
                "type": "object",
                "properties": {
                    "location": {
                        "type": "string",
                        "description": "City name or location"
                    }
                },
                "required": ["location"]
            }
        },
        {
            "name": "calculate",
            "description": "Perform a mathematical calculation",
            "input_schema": {
                "type": "object",
                "properties": {
                    "operation": {
                        "type": "string",
                        "enum": ["add", "subtract", "multiply", "divide"],
                        "description": "The operation to perform"
                    },
                    "a": {
                        "type": "number",
                        "description": "First number"
                    },
                    "b": {
                        "type": "number",
                        "description": "Second number"
                    }
                },
                "required": ["operation", "a", "b"]
            }
        }
    ]

    messages = [
        {
            "role": "user",
            "content": "What's the weather in San Francisco and calculate 15 + 27?"
        }
    ]

    print("Starting MCP Server interaction with Nexos Gateway...")
    print(f"User: {messages[0]['content']}\n")

    if not NEXOS_API_KEY:
        print("ERROR: No API key found")
        return

    headers = {
        "Authorization": f"Bearer {NEXOS_API_KEY}",
        "content-type": "application/json"
    }

    while True:
        payload = {
            "model": "Claude Opus 5.5",
            "max_tokens": 1024,
            "tools": tools,
            "messages": messages
        }

        try:
            with httpx.Client() as client:
                response = client.post(
                    f"{NEXOS_BASE_URL}/messages",
                    json=payload,
                    headers=headers
                )
                response.raise_for_status()
                data = response.json()
        except httpx.HTTPError as e:
            print(f"HTTP Error: {e}")
            print(f"Response: {e.response.text if hasattr(e, 'response') else 'N/A'}")
            break

        stop_reason = data.get("stop_reason")

        if stop_reason == "tool_use":
            tool_results = []
            for content_block in data.get("content", []):
                if content_block.get("type") == "tool_use":
                    tool_name = content_block.get("name")
                    tool_input = content_block.get("input", {})
                    tool_use_id = content_block.get("id")

                    print(f"Tool call: {tool_name}")
                    print(f"Input: {json.dumps(tool_input, indent=2)}")

                    result = process_tool_call(tool_name, tool_input)

                    print(f"Result: {result}\n")

                    tool_results.append({
                        "type": "tool_result",
                        "tool_use_id": tool_use_id,
                        "content": result
                    })

            messages.append({"role": "assistant", "content": data.get("content", [])})
            messages.append({"role": "user", "content": tool_results})

        elif stop_reason == "end_turn":
            for content_block in data.get("content", []):
                if content_block.get("type") == "text":
                    print(f"Assistant: {content_block.get('text')}")
            break

        else:
            print(f"Unexpected stop reason: {stop_reason}")
            break

    print("\nMCP Server interaction complete.")


if __name__ == "__main__":
    run_mcp_server()
